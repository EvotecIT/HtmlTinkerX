namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Seeds origin-scoped web storage once per capture before source scripts run.</summary>
internal static class HtmlBrowserStorageInitializer {
    private static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(2);
    private const string ScriptResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserStorageInitialization.js";
    private static readonly Lazy<string> Script = new(LoadScript, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static async Task<HtmlBrowserStorageInitialization?> AddAsync(
        IPage page,
        HtmlBrowserPdfRequest request,
        Action? cleanupTimedOut = null,
        TimeSpan? cleanupTimeout = null) {
        if (request.LocalStorage.Count == 0 && request.SessionStorage.Count == 0) return null;

        string statusKeyValue = "htmltinkerxStorage" + Guid.NewGuid().ToString("N");
        string worldName = "HtmlTinkerX.Storage." + Guid.NewGuid().ToString("N");
        string configuration = JsonSerializer.Serialize(new {
            expectedOrigin = request.Source.SecurityOrigin!.GetLeftPart(UriPartial.Authority),
            marker = "__htmltinkerx_seed_" + Guid.NewGuid().ToString("N"),
            statusKey = statusKeyValue,
            local = request.LocalStorage,
            session = request.SessionStorage
        });
        string script = $"({Script.Value})({configuration})";
        ICDPSession session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        HtmlBrowserStorageInitialization? initialization = null;
        try {
            await session.SendAsync("Page.enable").ConfigureAwait(false);
            await session.SendAsync("Runtime.enable").ConfigureAwait(false);
            string mainFrameId = await GetMainFrameIdAsync(session).ConfigureAwait(false);
            initialization = new HtmlBrowserStorageInitialization(
                session,
                worldName,
                statusKeyValue,
                mainFrameId,
                cleanupTimedOut,
                cleanupTimeout ?? DefaultCleanupTimeout);
            await session.SendAsync("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object> {
                ["source"] = script,
                ["worldName"] = worldName,
                ["runImmediately"] = true
            }).ConfigureAwait(false);
            return initialization;
        } catch {
            if (initialization != null) {
                await initialization.DisposeAsync().ConfigureAwait(false);
            } else {
                try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            }
            throw;
        }
    }

    private static async Task<string> GetMainFrameIdAsync(ICDPSession session) {
        JsonElement? tree = await session.SendAsync("Page.getFrameTree").ConfigureAwait(false);
        if (!tree.HasValue
            || !tree.Value.TryGetProperty("frameTree", out JsonElement frameTree)
            || !frameTree.TryGetProperty("frame", out JsonElement frame)
            || !frame.TryGetProperty("id", out JsonElement id)
            || string.IsNullOrEmpty(id.GetString())) {
            throw new PlaywrightException("Chromium did not report the main frame for web-storage initialization.");
        }
        return id.GetString()!;
    }

    private static string LoadScript() {
        Assembly assembly = typeof(HtmlBrowserStorageInitializer).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException($"Embedded browser script '{ScriptResource}' was not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

internal sealed class HtmlBrowserStorageInitialization : IAsyncDisposable {
    private readonly ICDPSession _session;
    private readonly string _worldName;
    private readonly string _statusKey;
    private readonly string _mainFrameId;
    private readonly ICDPSessionEvent _contextCreatedEvent;
    private readonly Action _cleanupTimedOut;
    private readonly TimeSpan _cleanupTimeout;
    private int _executionContextId;
    private int _disposed;

    internal HtmlBrowserStorageInitialization(
        ICDPSession session,
        string worldName,
        string statusKey,
        string mainFrameId,
        Action? cleanupTimedOut = null,
        TimeSpan? cleanupTimeout = null) {
        _session = session;
        _worldName = worldName;
        _statusKey = statusKey;
        _mainFrameId = mainFrameId;
        _cleanupTimedOut = cleanupTimedOut ?? NoOp;
        _cleanupTimeout = cleanupTimeout ?? TimeSpan.FromSeconds(2);
        _contextCreatedEvent = session.Event("Runtime.executionContextCreated");
        _contextCreatedEvent.OnEvent += HandleExecutionContextCreated;
    }

    internal async Task ValidateAsync() {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(HtmlBrowserStorageInitialization));
        int contextId = Volatile.Read(ref _executionContextId);
        if (contextId == 0) throw new InvalidOperationException("Requested web storage was not initialized for the capture origin.");
        string statusKey = JsonSerializer.Serialize(_statusKey);
        JsonElement? evaluation = await _session.SendAsync("Runtime.evaluate", new Dictionary<string, object> {
            ["expression"] = $"globalThis[{statusKey}] || null",
            ["contextId"] = contextId,
            ["returnByValue"] = true
        }).ConfigureAwait(false);
        if (evaluation.HasValue && evaluation.Value.TryGetProperty("exceptionDetails", out JsonElement exception)) {
            throw new PlaywrightException("Web-storage validation failed in Chromium's isolated world: " + exception.ToString());
        }
        string? json = evaluation.HasValue
            && evaluation.Value.TryGetProperty("result", out JsonElement result)
            && result.TryGetProperty("value", out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        HtmlBrowserStorageSeedStatus? status = json == null
            ? null
            : JsonSerializer.Deserialize<HtmlBrowserStorageSeedStatus>(json);
        if (status == null || !status.Completed) {
            throw new InvalidOperationException("Requested web storage was not initialized for the capture origin.");
        }
        if (status.Errors.Count > 0) {
            throw new InvalidOperationException("Requested web storage could not be initialized: " + string.Join("; ", status.Errors));
        }
    }

    private void HandleExecutionContextCreated(object? sender, JsonElement? payload) {
        if (!payload.HasValue
            || !payload.Value.TryGetProperty("context", out JsonElement context)
            || !context.TryGetProperty("name", out JsonElement name)
            || !string.Equals(name.GetString(), _worldName, StringComparison.Ordinal)
            || !context.TryGetProperty("auxData", out JsonElement auxiliaryData)
            || !auxiliaryData.TryGetProperty("frameId", out JsonElement frameId)
            || !string.Equals(frameId.GetString(), _mainFrameId, StringComparison.Ordinal)
            || !context.TryGetProperty("id", out JsonElement id)
            || !id.TryGetInt32(out int contextId)) return;
        Volatile.Write(ref _executionContextId, contextId);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _contextCreatedEvent.OnEvent -= HandleExecutionContextCreated;
        Task cleanup = _session.DetachAsync();
        if (await Task.WhenAny(cleanup, Task.Delay(_cleanupTimeout)).ConfigureAwait(false) == cleanup) {
            try { await cleanup.ConfigureAwait(false); } catch (PlaywrightException) { }
            return;
        }
        _cleanupTimedOut();
        ObserveLateFault(cleanup);
    }

    private static void ObserveLateFault(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void NoOp() { }

    private sealed class HtmlBrowserStorageSeedStatus {
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();
    }
}
