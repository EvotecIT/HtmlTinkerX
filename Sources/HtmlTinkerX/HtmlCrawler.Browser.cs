using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static async Task<CrawlRenderSession> CreateRenderSessionAsync(HtmlCrawlOptions options, Uri origin, CancellationToken cancellationToken) {
        if (options.Headers.Count > 0 && options.Browser != HtmlBrowserEngine.Chromium) {
            throw new NotSupportedException("Rendered crawls with custom HTTP headers require Chromium so headers can remain scoped to the starting origin across redirects. Use Chromium, static HTTP crawling, or render without custom headers.");
        }
        bool imported = !string.IsNullOrEmpty(options.StorageStatePath);
        Dictionary<string, string> headers = new(options.Headers, StringComparer.OrdinalIgnoreCase);
        if (!imported && options.FormLogin == null && options.Browser == HtmlBrowserEngine.Chromium
            && !string.IsNullOrEmpty(options.Username) && options.Password != null) {
            headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
        }
        HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
            Browser = options.Browser, Clean = options.CleanBrowserInstall, Headless = options.Headless,
            Username = imported || (options.FormLogin == null && options.Browser == HtmlBrowserEngine.Chromium) ? null : options.Username,
            Password = imported || (options.FormLogin == null && options.Browser == HtmlBrowserEngine.Chromium) ? null : options.Password,
            FormLogin = imported ? null : options.FormLogin, StorageStatePath = options.StorageStatePath,
            UserAgent = options.UserAgent, Proxy = options.Proxy, ProxyUsername = options.ProxyUsername,
            ProxyPassword = options.ProxyPassword, Timeout = options.Timeout,
            HttpCredentialOrigin = origin.GetLeftPart(UriPartial.Authority), BlockServiceWorkers = headers.Count > 0
        }, cancellationToken).ConfigureAwait(false);
        CrawlRenderSession owner = new(session);
        try {
            session.Page.SetDefaultTimeout(options.Timeout);
            if (options.Browser == HtmlBrowserEngine.Chromium && (headers.Count > 0 || options.RestrictToHost)) {
                owner.Headers = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(session.Context, session.Page,
                    origin, headers, cancellationToken, requestAllowed: (url, topLevel) => Task.FromResult(
                        !topLevel || (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsCrawlHostAllowed(uri, origin, options)))).ConfigureAwait(false);
                if (headers.Count > 0) {
                    owner.Popups = new HtmlBrowserPopupHeaderCoordinator(session.Context, session.Page, origin, headers,
                        cancellationToken, static () => { });
                    await owner.Popups.AddNavigationShimAsync(session.Page).ConfigureAwait(false);
                }
            }
            foreach (string pattern in options.BlockResourcePatterns) {
                await HtmlBrowser.RegisterRouteAsync(session, pattern, route => route.AbortAsync(), cancellationToken).ConfigureAwait(false);
            }
            return owner;
        } catch {
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class CrawlRenderSession(HtmlBrowserSession session) : IAsyncDisposable {
        public HtmlBrowserSession Session { get; } = session;
        public HtmlBrowserScopedHeaderInterceptor? Headers { get; set; }
        public HtmlBrowserPopupHeaderCoordinator? Popups { get; set; }
        public void ThrowIfFaulted() { Headers?.ThrowIfFaulted(); Popups?.ThrowIfFaulted(); }
        public async ValueTask DisposeAsync() {
            try {
                try {
                    if (Popups != null) await Popups.DisposeAsync().ConfigureAwait(false);
                } finally {
                    if (Headers != null) await Headers.DisposeAsync().ConfigureAwait(false);
                }
            } finally {
                await Session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
