namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Attaches streaming header interception to popups and bridges only their initial document request.</summary>
internal sealed class HtmlBrowserPopupHeaderCoordinator : IAsyncDisposable {
    private readonly IBrowserContext _context;
    private readonly IPage _primaryPage;
    private readonly Uri? _origin;
    private readonly IReadOnlyDictionary<string, string> _captureHeaders;
    private readonly CancellationToken _cancellationToken;
    private readonly ConcurrentDictionary<IPage, HtmlBrowserScopedHeaderInterceptor> _interceptors = new();
    private readonly ConcurrentDictionary<Task, byte> _pending = new();
    private readonly EventHandler<IPage> _pageHandler;
    private int _disposed;

    internal HtmlBrowserPopupHeaderCoordinator(
        IBrowserContext context,
        IPage primaryPage,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        CancellationToken cancellationToken) {
        _context = context;
        _primaryPage = primaryPage;
        _origin = origin;
        _captureHeaders = captureHeaders;
        _cancellationToken = cancellationToken;
        _pageHandler = OnPage;
        context.Page += _pageHandler;
    }

    internal static Task AddNavigationShimAsync(IPage page) => page.AddInitScriptAsync(@"
        (() => {
            const originalOpen = window.open;
            window.open = function(url, target, features) {
                if (url == null || String(url).length === 0 || String(url).toLowerCase() === 'about:blank') {
                    return originalOpen.call(this, url, target, features);
                }
                const destination = new URL(String(url), document.baseURI).href;
                const featureTokens = features == null
                    ? []
                    : String(features).split(',').map(token => token.trim()).filter(Boolean);
                const isEnabled = name => featureTokens.some(token => {
                    const parts = token.toLowerCase().split('=', 2);
                    return parts[0] === name && (parts.length === 1 || !['0', 'no', 'false'].includes(parts[1]));
                });
                const suppressReferrer = isEnabled('noreferrer');
                const suppressOpener = suppressReferrer || isEnabled('noopener');
                const initialFeatures = suppressOpener
                    ? featureTokens.filter(token => !['noopener', 'noreferrer'].includes(token.toLowerCase().split('=', 1)[0])).join(',')
                    : features;
                const popup = originalOpen.call(this, 'about:blank', target, initialFeatures);
                if (popup) {
                    popup.setTimeout(() => {
                        try {
                            if (suppressReferrer) {
                                const link = popup.document.createElement('a');
                                link.href = destination;
                                link.rel = 'noreferrer';
                                link.target = '_self';
                                (popup.document.body || popup.document.documentElement).appendChild(link);
                                link.click();
                            } else {
                                popup.location.href = destination;
                            }
                        } catch { }
                    }, 0);
                    if (suppressOpener) {
                        try { popup.opener = null; } catch { }
                    }
                }
                return suppressOpener ? null : popup;
            };
        })();");

    internal bool RequiresDocumentBridge(IRequest request) {
        if (Volatile.Read(ref _disposed) != 0
            || !string.Equals(request.ResourceType, "document", StringComparison.OrdinalIgnoreCase)) return false;
        return IsSameOrigin(_origin, request.Url);
    }

    internal async Task ContinueInitialDocumentAsync(IRoute route) {
        Dictionary<string, string> headers = new(route.Request.Headers, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> header in _captureHeaders) headers[header.Key] = header.Value;
        IAPIResponse response = await route.FetchAsync(new RouteFetchOptions {
            Headers = headers,
            MaxRedirects = 0
        }).ConfigureAwait(false);
        try {
            await route.FulfillAsync(new RouteFulfillOptions { Response = response }).ConfigureAwait(false);
        } finally {
            await response.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OnPage(object? sender, IPage page) {
        if (page == _primaryPage || Volatile.Read(ref _disposed) != 0) return;
        Task pending = AttachAsync(page);
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

    private async Task AttachAsync(IPage page) {
        HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            _context,
            page,
            _origin,
            _captureHeaders,
            _cancellationToken).ConfigureAwait(false);
        if (!_interceptors.TryAdd(page, interceptor)) await interceptor.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _context.Page -= _pageHandler;
        Task[] pending = _pending.Keys.ToArray();
        if (pending.Length > 0) {
            try { await Task.WhenAll(pending).ConfigureAwait(false); } catch (Exception) { }
        }
        foreach (HtmlBrowserScopedHeaderInterceptor interceptor in _interceptors.Values) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
        }
        _interceptors.Clear();
    }

    private static bool IsSameOrigin(Uri? expectedOrigin, string requestUrl) {
        if (expectedOrigin == null || !Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri)) return false;
        return string.Equals(expectedOrigin.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedOrigin.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase)
            && expectedOrigin.Port == requestUri.Port;
    }
}
