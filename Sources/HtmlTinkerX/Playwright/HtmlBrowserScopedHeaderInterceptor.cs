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
    private readonly ConcurrentDictionary<string, byte> _workerSessions = new(StringComparer.Ordinal);
    private readonly EventHandler<JsonElement?> _handler;
    private readonly EventHandler<JsonElement?> _targetAttachedHandler;
    private readonly EventHandler<JsonElement?> _targetMessageHandler;
    private readonly EventHandler<JsonElement?> _targetDetachedHandler;
    private readonly TimeSpan _cleanupTimeout;
    private readonly Action _cleanupTimedOut;
    private long _nextWorkerCommandId;
    private int _disposed;
    private int _subscribed;

    internal HtmlBrowserScopedHeaderInterceptor(
        ICDPSession session,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        TimeSpan? cleanupTimeout = null,
        Action? cleanupTimedOut = null) {
        _session = session;
        _origin = origin;
        _captureHeaders = captureHeaders;
        _cleanupTimeout = cleanupTimeout ?? DefaultCleanupTimeout;
        _cleanupTimedOut = cleanupTimedOut ?? NoOp;
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
        TimeSpan? cleanupTimeout = null) {
        cancellationToken.ThrowIfCancellationRequested();
        ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
        HtmlBrowserScopedHeaderInterceptor interceptor = new(session, origin, captureHeaders, cleanupTimeout, cleanupTimedOut);
        interceptor.Subscribe();
        try {
            await session.SendAsync("Fetch.enable", new Dictionary<string, object> {
                ["patterns"] = CreateFetchPatterns()
            }).ConfigureAwait(false);
            await session.SendAsync("Target.setAutoAttach", new Dictionary<string, object> {
                ["autoAttach"] = true,
                ["waitForDebuggerOnStart"] = true,
                ["flatten"] = false,
                ["filter"] = new object[] {
                    new Dictionary<string, object> { ["type"] = "worker", ["exclude"] = false },
                    new Dictionary<string, object> { ["exclude"] = true }
                }
            }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return interceptor;
        } catch {
            interceptor.Unsubscribe();
            try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
    }

    private void OnRequestPaused(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        Track(ContinueRequestAsync(payload.Value, workerSessionId: null));
    }

    private void OnTargetAttached(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        JsonElement targetInfo = payload.Value.GetProperty("targetInfo");
        if (!string.Equals(targetInfo.GetProperty("type").GetString(), "worker", StringComparison.Ordinal)) return;
        string sessionId = payload.Value.GetProperty("sessionId").GetString()!;
        _workerSessions[sessionId] = 0;
        Track(ConfigureWorkerAsync(sessionId));
    }

    private void OnTargetMessage(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        string sessionId = payload.Value.GetProperty("sessionId").GetString()!;
        if (!_workerSessions.ContainsKey(sessionId)) return;
        using JsonDocument message = JsonDocument.Parse(payload.Value.GetProperty("message").GetString()!);
        if (!message.RootElement.TryGetProperty("method", out JsonElement method)
            || !string.Equals(method.GetString(), "Fetch.requestPaused", StringComparison.Ordinal)
            || !message.RootElement.TryGetProperty("params", out JsonElement parameters)) return;
        Track(ContinueRequestAsync(parameters.Clone(), sessionId));
    }

    private void OnTargetDetached(object? sender, JsonElement? payload) {
        if (payload == null || !payload.Value.TryGetProperty("sessionId", out JsonElement session)) return;
        string? sessionId = session.GetString();
        if (sessionId != null) _workerSessions.TryRemove(sessionId, out _);
    }

    private void Track(Task pending) {
        _pending[pending] = 0;
        _ = pending.ContinueWith(
            completed => {
                _pending.TryRemove(completed, out _);
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ConfigureWorkerAsync(string sessionId) {
        try {
            await SendWorkerCommandAsync(sessionId, "Fetch.enable", new Dictionary<string, object> {
                ["patterns"] = CreateFetchPatterns()
            }).ConfigureAwait(false);
        } finally {
            await SendWorkerCommandAsync(sessionId, "Runtime.runIfWaitingForDebugger", new Dictionary<string, object>()).ConfigureAwait(false);
        }
    }

    private static object[] CreateFetchPatterns() => new object[] {
        new Dictionary<string, object> { ["urlPattern"] = "http://*/*", ["requestStage"] = "Request" },
        new Dictionary<string, object> { ["urlPattern"] = "https://*/*", ["requestStage"] = "Request" }
    };

    private async Task ContinueRequestAsync(JsonElement payload, string? workerSessionId) {
        string requestId = payload.GetProperty("requestId").GetString()!;
        try {
            JsonElement request = payload.GetProperty("request");
            string url = request.GetProperty("url").GetString()!;
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
            await SendCommandAsync(workerSessionId, "Fetch.continueRequest", continueArguments).ConfigureAwait(false);
        } catch (Exception) {
            try {
                await SendCommandAsync(workerSessionId, "Fetch.failRequest", new Dictionary<string, object> {
                    ["requestId"] = requestId,
                    ["errorReason"] = "Failed"
                }).ConfigureAwait(false);
            } catch (PlaywrightException) { }
            throw;
        }
    }

    private Task SendCommandAsync(string? workerSessionId, string method, Dictionary<string, object> arguments) =>
        workerSessionId == null
            ? _session.SendAsync(method, arguments)
            : SendWorkerCommandAsync(workerSessionId, method, arguments);

    private Task SendWorkerCommandAsync(string sessionId, string method, Dictionary<string, object> arguments) {
        string message = JsonSerializer.Serialize(new Dictionary<string, object> {
            ["id"] = Interlocked.Increment(ref _nextWorkerCommandId),
            ["method"] = method,
            ["params"] = arguments
        });
        return _session.SendAsync("Target.sendMessageToTarget", new Dictionary<string, object> {
            ["sessionId"] = sessionId,
            ["message"] = message
        });
    }

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
            await _session.SendAsync("Target.setAutoAttach", new Dictionary<string, object> {
                ["autoAttach"] = false,
                ["waitForDebuggerOnStart"] = false,
                ["flatten"] = false
            }).ConfigureAwait(false);
        } catch (PlaywrightException) { }
        try { await _session.SendAsync("Fetch.disable").ConfigureAwait(false); } catch (PlaywrightException) { }
        foreach (string workerSession in _workerSessions.Keys) {
            try { await SendWorkerCommandAsync(workerSession, "Fetch.disable", new Dictionary<string, object>()).ConfigureAwait(false); } catch (PlaywrightException) { }
            try {
                await _session.SendAsync("Target.detachFromTarget", new Dictionary<string, object> {
                    ["sessionId"] = workerSession
                }).ConfigureAwait(false);
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
