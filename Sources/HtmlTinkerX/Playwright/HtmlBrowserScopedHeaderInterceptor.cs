namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Injects capture-scoped Chromium request headers without buffering responses.</summary>
internal sealed class HtmlBrowserScopedHeaderInterceptor : IAsyncDisposable {
    private static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(2);
    private readonly ICDPSession _session;
    private readonly Uri? _origin;
    private readonly IReadOnlyDictionary<string, string> _captureHeaders;
    private readonly ConcurrentDictionary<Task, byte> _pending = new();
    private readonly ConcurrentDictionary<string, string[]> _workerSessions = new(StringComparer.Ordinal);
    private readonly EventHandler<JsonElement?> _handler;
    private readonly EventHandler<JsonElement?> _targetAttachedHandler;
    private readonly EventHandler<JsonElement?> _targetMessageHandler;
    private readonly EventHandler<JsonElement?> _targetDetachedHandler;
    private readonly TimeSpan _cleanupTimeout;
    private readonly Action _cleanupTimedOut;
    private readonly Func<string, Task<bool>>? _requestAllowed;
    private readonly Action<string, bool>? _requestBlocked;
    private readonly string? _mainFrameId;
    private Exception? _failure;
    private long _nextWorkerCommandId;
    private int _disposed;
    private int _subscribed;

    internal HtmlBrowserScopedHeaderInterceptor(
        ICDPSession session,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        TimeSpan? cleanupTimeout = null,
        Action? cleanupTimedOut = null,
        Func<string, Task<bool>>? requestAllowed = null,
        Action<string, bool>? requestBlocked = null,
        string? mainFrameId = null) {
        _session = session;
        _origin = origin;
        _captureHeaders = captureHeaders;
        _cleanupTimeout = cleanupTimeout ?? DefaultCleanupTimeout;
        _cleanupTimedOut = cleanupTimedOut ?? NoOp;
        _requestAllowed = requestAllowed;
        _requestBlocked = requestBlocked;
        _mainFrameId = mainFrameId;
        _handler = OnRequestPaused;
        _targetAttachedHandler = OnTargetAttached;
        _targetMessageHandler = OnTargetMessage;
        _targetDetachedHandler = OnTargetDetached;
    }

    internal static async Task<HtmlBrowserScopedHeaderInterceptor> CreateAsync(
        IBrowserContext context,
        IPage page,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        CancellationToken cancellationToken,
        Action? cleanupTimedOut = null,
        TimeSpan? cleanupTimeout = null,
        Func<string, Task<bool>>? requestAllowed = null,
        Action<string, bool>? requestBlocked = null) {
        cancellationToken.ThrowIfCancellationRequested();
        ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
        string? mainFrameId;
        try {
            mainFrameId = await GetMainFrameIdAsync(session).ConfigureAwait(false);
        } catch {
            try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
        HtmlBrowserScopedHeaderInterceptor interceptor = new(
            session,
            origin,
            captureHeaders,
            cleanupTimeout,
            cleanupTimedOut,
            requestAllowed,
            requestBlocked,
            mainFrameId);
        interceptor.Subscribe();
        try {
            await session.SendAsync("Fetch.enable", new Dictionary<string, object> {
                ["patterns"] = CreateFetchPatterns()
            }).ConfigureAwait(false);
            await session.SendAsync("Target.setAutoAttach", CreateAutoAttachArguments(enabled: true)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return interceptor;
        } catch {
            interceptor.Unsubscribe();
            try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
    }

    private static async Task<string?> GetMainFrameIdAsync(ICDPSession session) {
        JsonElement? result = await session.SendAsync("Page.getFrameTree").ConfigureAwait(false);
        if (!result.HasValue
            || !result.Value.TryGetProperty("frameTree", out JsonElement frameTree)
            || !frameTree.TryGetProperty("frame", out JsonElement frame)
            || !frame.TryGetProperty("id", out JsonElement id)) return null;
        return id.GetString();
    }

    private void OnRequestPaused(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        Track(ContinueRequestAsync(payload.Value, workerSessionPath: null));
    }

    private void OnTargetAttached(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        JsonElement targetInfo = payload.Value.GetProperty("targetInfo");
        if (!string.Equals(targetInfo.GetProperty("type").GetString(), "worker", StringComparison.Ordinal)) return;
        string sessionId = payload.Value.GetProperty("sessionId").GetString()!;
        RegisterWorker(new[] { sessionId });
    }

    private void OnTargetMessage(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        string sessionId = payload.Value.GetProperty("sessionId").GetString()!;
        if (!_workerSessions.TryGetValue(sessionId, out string[]? workerPath)) return;
        using JsonDocument message = JsonDocument.Parse(payload.Value.GetProperty("message").GetString()!);
        ProcessWorkerMessage(workerPath, message.RootElement);
    }

    private void OnTargetDetached(object? sender, JsonElement? payload) {
        if (payload == null || !payload.Value.TryGetProperty("sessionId", out JsonElement session)) return;
        string? sessionId = session.GetString();
        if (sessionId != null) RemoveWorkerAndDescendants(new[] { sessionId });
    }

    private void Track(Task pending) {
        _pending[pending] = 0;
        _ = pending.ContinueWith(
            completed => {
                _pending.TryRemove(completed, out _);
                if (!completed.IsFaulted || Volatile.Read(ref _disposed) != 0) return;
                Interlocked.CompareExchange(ref _failure, completed.Exception!.GetBaseException(), null);
                _cleanupTimedOut();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal void ThrowIfFaulted() {
        Exception? failure = Volatile.Read(ref _failure);
        if (failure != null) {
            throw new InvalidOperationException("Scoped header interception failed before capture completed.", failure);
        }
    }

    private void RegisterWorker(string[] workerPath) {
        string key = WorkerPathKey(workerPath);
        if (!_workerSessions.TryAdd(key, workerPath)) return;
        Track(ConfigureWorkerAsync(workerPath));
    }

    private void ProcessWorkerMessage(string[] workerPath, JsonElement message) {
        if (!message.TryGetProperty("method", out JsonElement methodElement)
            || !message.TryGetProperty("params", out JsonElement parameters)) return;
        string? method = methodElement.GetString();
        if (string.Equals(method, "Fetch.requestPaused", StringComparison.Ordinal)) {
            Track(ContinueRequestAsync(parameters.Clone(), workerPath));
            return;
        }
        if (string.Equals(method, "Target.attachedToTarget", StringComparison.Ordinal)
            && parameters.TryGetProperty("targetInfo", out JsonElement targetInfo)
            && string.Equals(targetInfo.GetProperty("type").GetString(), "worker", StringComparison.Ordinal)
            && parameters.TryGetProperty("sessionId", out JsonElement attachedSession)) {
            string? childSessionId = attachedSession.GetString();
            if (childSessionId != null) RegisterWorker(workerPath.Concat(new[] { childSessionId }).ToArray());
            return;
        }
        if (string.Equals(method, "Target.receivedMessageFromTarget", StringComparison.Ordinal)
            && parameters.TryGetProperty("sessionId", out JsonElement nestedSession)
            && parameters.TryGetProperty("message", out JsonElement nestedMessage)) {
            string? childSessionId = nestedSession.GetString();
            if (childSessionId == null) return;
            string[] childPath = workerPath.Concat(new[] { childSessionId }).ToArray();
            if (!_workerSessions.ContainsKey(WorkerPathKey(childPath))) return;
            using JsonDocument child = JsonDocument.Parse(nestedMessage.GetString()!);
            ProcessWorkerMessage(childPath, child.RootElement);
            return;
        }
        if (string.Equals(method, "Target.detachedFromTarget", StringComparison.Ordinal)
            && parameters.TryGetProperty("sessionId", out JsonElement detachedSession)) {
            string? childSessionId = detachedSession.GetString();
            if (childSessionId != null) RemoveWorkerAndDescendants(workerPath.Concat(new[] { childSessionId }).ToArray());
        }
    }

    private void RemoveWorkerAndDescendants(IReadOnlyList<string> workerPath) {
        foreach (KeyValuePair<string, string[]> worker in _workerSessions) {
            if (worker.Value.Length < workerPath.Count) continue;
            bool matches = true;
            for (int index = 0; index < workerPath.Count; index++) {
                if (string.Equals(worker.Value[index], workerPath[index], StringComparison.Ordinal)) continue;
                matches = false;
                break;
            }
            if (matches) _workerSessions.TryRemove(worker.Key, out _);
        }
    }

    private async Task ConfigureWorkerAsync(string[] workerPath) {
        try {
            await SendWorkerCommandAsync(workerPath, "Fetch.enable", new Dictionary<string, object> {
                ["patterns"] = CreateFetchPatterns()
            }).ConfigureAwait(false);
            await SendWorkerCommandAsync(workerPath, "Target.setAutoAttach", CreateAutoAttachArguments(enabled: true)).ConfigureAwait(false);
        } finally {
            await SendWorkerCommandAsync(workerPath, "Runtime.runIfWaitingForDebugger", new Dictionary<string, object>()).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, object> CreateAutoAttachArguments(bool enabled) => new() {
        ["autoAttach"] = enabled,
        ["waitForDebuggerOnStart"] = enabled,
        ["flatten"] = false,
        ["filter"] = new object[] {
            new Dictionary<string, object> { ["type"] = "worker", ["exclude"] = false },
            new Dictionary<string, object> { ["exclude"] = true }
        }
    };

    private static object[] CreateFetchPatterns() => new object[] {
        new Dictionary<string, object> { ["urlPattern"] = "http://*/*", ["requestStage"] = "Request" },
        new Dictionary<string, object> { ["urlPattern"] = "https://*/*", ["requestStage"] = "Request" }
    };

    private async Task ContinueRequestAsync(JsonElement payload, IReadOnlyList<string>? workerSessionPath) {
        string requestId = payload.GetProperty("requestId").GetString()!;
        try {
            JsonElement request = payload.GetProperty("request");
            string url = request.GetProperty("url").GetString()!;
            if (_requestAllowed != null && !await _requestAllowed(url).ConfigureAwait(false)) {
                bool documentRequest = payload.TryGetProperty("resourceType", out JsonElement resourceType)
                    && string.Equals(resourceType.GetString(), "Document", StringComparison.OrdinalIgnoreCase);
                bool topLevelDocument = documentRequest
                    && payload.TryGetProperty("frameId", out JsonElement frameId)
                    && string.Equals(frameId.GetString(), _mainFrameId, StringComparison.Ordinal);
                _requestBlocked?.Invoke(url, topLevelDocument);
                if (documentRequest) {
                    await SendCommandAsync(workerSessionPath, "Fetch.fulfillRequest", new Dictionary<string, object> {
                        ["requestId"] = requestId,
                        ["responseCode"] = 204,
                        ["responseHeaders"] = Array.Empty<object>()
                    }).ConfigureAwait(false);
                } else {
                    await SendCommandAsync(workerSessionPath, "Fetch.failRequest", new Dictionary<string, object> {
                        ["requestId"] = requestId,
                        ["errorReason"] = "BlockedByClient"
                    }).ConfigureAwait(false);
                }
                return;
            }
            Dictionary<string, object> continueArguments = new() { ["requestId"] = requestId };
            if (IsSameOrigin(_origin, url)) {
                Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty header in request.GetProperty("headers").EnumerateObject()) {
                    headers[header.Name] = header.Value.GetString() ?? string.Empty;
                }
                foreach (KeyValuePair<string, string> header in _captureHeaders) headers[header.Key] = header.Value;
                continueArguments["headers"] = headers.Select(header => (object)new Dictionary<string, object> {
                    ["name"] = header.Key,
                    ["value"] = header.Value
                }).ToArray();
            }
            await SendCommandAsync(workerSessionPath, "Fetch.continueRequest", continueArguments).ConfigureAwait(false);
        } catch (Exception) {
            try {
                await SendCommandAsync(workerSessionPath, "Fetch.failRequest", new Dictionary<string, object> {
                    ["requestId"] = requestId,
                    ["errorReason"] = "Failed"
                }).ConfigureAwait(false);
            } catch (PlaywrightException) { }
            throw;
        }
    }

    private Task SendCommandAsync(IReadOnlyList<string>? workerSessionPath, string method, Dictionary<string, object> arguments) =>
        workerSessionPath == null
            ? _session.SendAsync(method, arguments)
            : SendWorkerCommandAsync(workerSessionPath, method, arguments);

    private Task SendWorkerCommandAsync(IReadOnlyList<string> workerPath, string method, Dictionary<string, object> arguments) {
        string message = JsonSerializer.Serialize(new Dictionary<string, object> {
            ["id"] = Interlocked.Increment(ref _nextWorkerCommandId),
            ["method"] = method,
            ["params"] = arguments
        });
        for (int index = workerPath.Count - 1; index >= 1; index--) {
            message = JsonSerializer.Serialize(new Dictionary<string, object> {
                ["id"] = Interlocked.Increment(ref _nextWorkerCommandId),
                ["method"] = "Target.sendMessageToTarget",
                ["params"] = new Dictionary<string, object> {
                    ["sessionId"] = workerPath[index],
                    ["message"] = message
                }
            });
        }
        return _session.SendAsync("Target.sendMessageToTarget", new Dictionary<string, object> {
            ["sessionId"] = workerPath[0],
            ["message"] = message
        });
    }

    private static string WorkerPathKey(IEnumerable<string> workerPath) => string.Join("/", workerPath);

    private static bool IsSameOrigin(Uri? expectedOrigin, string requestUrl) {
        if (expectedOrigin == null || !Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri)) return false;
        return string.Equals(expectedOrigin.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedOrigin.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase)
            && expectedOrigin.Port == requestUri.Port;
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Unsubscribe();
        Task cleanup = DisposeCoreAsync();
        if (await Task.WhenAny(cleanup, Task.Delay(_cleanupTimeout)).ConfigureAwait(false) == cleanup) {
            await cleanup.ConfigureAwait(false);
            return;
        }
        _cleanupTimedOut();
        ObserveLateFault(cleanup);
    }

    private async Task DisposeCoreAsync() {
        try {
            await _session.SendAsync("Target.setAutoAttach", CreateAutoAttachArguments(enabled: false)).ConfigureAwait(false);
        } catch (PlaywrightException) { }
        try { await _session.SendAsync("Fetch.disable").ConfigureAwait(false); } catch (PlaywrightException) { }
        foreach (string[] workerPath in _workerSessions.Values.OrderByDescending(path => path.Length)) {
            try { await SendWorkerCommandAsync(workerPath, "Target.setAutoAttach", CreateAutoAttachArguments(enabled: false)).ConfigureAwait(false); } catch (PlaywrightException) { }
            try { await SendWorkerCommandAsync(workerPath, "Fetch.disable", new Dictionary<string, object>()).ConfigureAwait(false); } catch (PlaywrightException) { }
            try {
                if (workerPath.Length == 1) {
                    await _session.SendAsync("Target.detachFromTarget", new Dictionary<string, object> {
                        ["sessionId"] = workerPath[0]
                    }).ConfigureAwait(false);
                } else {
                    await SendWorkerCommandAsync(workerPath.Take(workerPath.Length - 1).ToArray(), "Target.detachFromTarget", new Dictionary<string, object> {
                        ["sessionId"] = workerPath[workerPath.Length - 1]
                    }).ConfigureAwait(false);
                }
            } catch (PlaywrightException) { }
        }
        _workerSessions.Clear();
        Task[] pending = _pending.Keys.ToArray();
        if (pending.Length > 0) {
            try { await Task.WhenAll(pending).ConfigureAwait(false); } catch (PlaywrightException) { }
        }
        try { await _session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
    }

    private static void ObserveLateFault(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void NoOp() { }

    private void Subscribe() {
        if (Interlocked.Exchange(ref _subscribed, 1) != 0) return;
        _session.Event("Fetch.requestPaused").OnEvent += _handler;
        _session.Event("Target.attachedToTarget").OnEvent += _targetAttachedHandler;
        _session.Event("Target.receivedMessageFromTarget").OnEvent += _targetMessageHandler;
        _session.Event("Target.detachedFromTarget").OnEvent += _targetDetachedHandler;
    }

    private void Unsubscribe() {
        if (Interlocked.Exchange(ref _subscribed, 0) == 0) return;
        _session.Event("Fetch.requestPaused").OnEvent -= _handler;
        _session.Event("Target.attachedToTarget").OnEvent -= _targetAttachedHandler;
        _session.Event("Target.receivedMessageFromTarget").OnEvent -= _targetMessageHandler;
        _session.Event("Target.detachedFromTarget").OnEvent -= _targetDetachedHandler;
    }
}
