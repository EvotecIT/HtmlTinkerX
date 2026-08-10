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
    private readonly ICDPSession _session;
    private readonly Uri? _origin;
    private readonly IReadOnlyDictionary<string, string> _captureHeaders;
    private readonly ConcurrentDictionary<Task, byte> _pending = new();
    private readonly EventHandler<JsonElement?> _handler;
    private int _disposed;

    private HtmlBrowserScopedHeaderInterceptor(
        ICDPSession session,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders) {
        _session = session;
        _origin = origin;
        _captureHeaders = captureHeaders;
        _handler = OnRequestPaused;
    }

    internal static async Task<HtmlBrowserScopedHeaderInterceptor> CreateAsync(
        IBrowserContext context,
        IPage page,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
        HtmlBrowserScopedHeaderInterceptor interceptor = new(session, origin, captureHeaders);
        session.Event("Fetch.requestPaused").OnEvent += interceptor._handler;
        try {
            await session.SendAsync("Fetch.enable", new Dictionary<string, object> {
                ["patterns"] = new[] {
                    new Dictionary<string, object> { ["urlPattern"] = "http://*/*", ["requestStage"] = "Request" },
                    new Dictionary<string, object> { ["urlPattern"] = "https://*/*", ["requestStage"] = "Request" }
                }
            }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return interceptor;
        } catch {
            session.Event("Fetch.requestPaused").OnEvent -= interceptor._handler;
            try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
    }

    private void OnRequestPaused(object? sender, JsonElement? payload) {
        if (payload == null || Volatile.Read(ref _disposed) != 0) return;
        Task pending = ContinueRequestAsync(payload.Value);
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

    private async Task ContinueRequestAsync(JsonElement payload) {
        string requestId = payload.GetProperty("requestId").GetString()!;
        try {
            JsonElement request = payload.GetProperty("request");
            string url = request.GetProperty("url").GetString()!;
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty header in request.GetProperty("headers").EnumerateObject()) {
                headers[header.Name] = header.Value.GetString() ?? string.Empty;
            }
            foreach (string name in _captureHeaders.Keys) headers.Remove(name);
            if (IsSameOrigin(_origin, url)) {
                foreach (KeyValuePair<string, string> header in _captureHeaders) headers[header.Key] = header.Value;
            }
            object[] cdpHeaders = headers.Select(header => (object)new Dictionary<string, object> {
                ["name"] = header.Key,
                ["value"] = header.Value
            }).ToArray();
            await _session.SendAsync("Fetch.continueRequest", new Dictionary<string, object> {
                ["requestId"] = requestId,
                ["headers"] = cdpHeaders
            }).ConfigureAwait(false);
        } catch (Exception) {
            try {
                await _session.SendAsync("Fetch.failRequest", new Dictionary<string, object> {
                    ["requestId"] = requestId,
                    ["errorReason"] = "Failed"
                }).ConfigureAwait(false);
            } catch (PlaywrightException) {
                // The target can close while a paused request is being failed.
            }
            throw;
        }
    }

    private static bool IsSameOrigin(Uri? expectedOrigin, string requestUrl) {
        if (expectedOrigin == null || !Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri)) return false;
        return string.Equals(expectedOrigin.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedOrigin.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase)
            && expectedOrigin.Port == requestUri.Port;
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _session.Event("Fetch.requestPaused").OnEvent -= _handler;
        try { await _session.SendAsync("Fetch.disable").ConfigureAwait(false); } catch (PlaywrightException) { }
        Task[] pending = _pending.Keys.ToArray();
        if (pending.Length > 0) {
            try { await Task.WhenAll(pending).ConfigureAwait(false); } catch (PlaywrightException) { }
        }
        try { await _session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
    }
}
