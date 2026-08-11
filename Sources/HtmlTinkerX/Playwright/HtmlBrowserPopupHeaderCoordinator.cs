namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Attaches streaming header interception before newly opened popups navigate.</summary>
internal sealed class HtmlBrowserPopupHeaderCoordinator : IAsyncDisposable {
    private const string ReleaseNavigationScript = @"() => {
        globalThis.__htmlTinkerXPopupHeadersReady = true;
        const release = globalThis.__htmlTinkerXReleasePopupNavigation;
        if (typeof release === 'function') release();
    }";
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
                // An empty URL creates an interceptable about:blank popup without navigating an
                // existing _self/_parent/_top or named context before deferred navigation runs.
                const popup = originalOpen.call(this, '', target, initialFeatures);
                if (popup) {
                    if (suppressOpener) {
                        try { popup.opener = null; } catch { }
                    }
                    const navigate = () => {
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
                        } catch {
                            // Existing named contexts can still be cross-origin. Their Location
                            // is normally writable, but native navigation is the standards-safe
                            // fallback when the WindowProxy does not expose the required surface.
                            originalOpen.call(window, destination, target, features);
                        }
                    };
                    let currentUrl;
                    try { currentUrl = popup.location.href; } catch { currentUrl = null; }
                    const normalizedTarget = target == null || String(target).length === 0
                        ? '_blank'
                        : String(target).toLowerCase();
                    const isNewBlankContext = currentUrl === 'about:blank'
                        && !['_self', '_parent', '_top'].includes(normalizedTarget);
                    if (isNewBlankContext) {
                        let fallback;
                        const release = () => {
                            if (fallback != null) globalThis.clearTimeout(fallback);
                            try { delete popup.__htmlTinkerXReleasePopupNavigation; } catch { }
                            navigate();
                        };
                        popup.__htmlTinkerXReleasePopupNavigation = release;
                        if (popup.__htmlTinkerXPopupHeadersReady === true) {
                            release();
                        } else if (normalizedTarget !== '_blank') {
                            // An existing named about:blank context does not raise a new-page
                            // event. Preserve its navigation after a short compatibility delay.
                            fallback = globalThis.setTimeout(release, 1000);
                        }
                    } else {
                        globalThis.setTimeout(navigate, 0);
                    }
                }
                return suppressOpener ? null : popup;
            };
        })();");

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
        if (!_interceptors.TryAdd(page, interceptor)) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            await page.EvaluateAsync(ReleaseNavigationScript).ConfigureAwait(false);
        } catch (PlaywrightException) {
            // A caller can close or replace the blank popup while interception is attached.
        }
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
}
