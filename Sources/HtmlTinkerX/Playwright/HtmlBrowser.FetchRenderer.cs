using Microsoft.Playwright;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Creates an <see cref="HtmlFetchPolicy.Renderer"/> that loads a blocked page in a browser and returns its rendered
    /// HTML and final address. Each call opens and closes one session. Set
    /// <see cref="HtmlBrowserLaunchOptions.CdpEndpointUrl"/> to read through the operator's own running Chrome, whose
    /// profile and reputation a CDN may already trust.
    /// </summary>
    /// <remarks>
    /// A page the browser was refused too (a 401, 403, 429 or 5xx navigation, a recognised block page or an unsolved
    /// Cloudflare challenge) throws <see cref="HtmlHttpException"/> instead of being returned as content.
    /// </remarks>
    /// <param name="options">Browser launch options; headless Chromium with network-idle loading when null.</param>
    public static Func<Uri, CancellationToken, Task<HtmlRenderedPage>> CreateFetchRenderer(HtmlBrowserLaunchOptions? options = null) {
        HtmlBrowserLaunchOptions launch = options ?? new HtmlBrowserLaunchOptions();
        return async (uri, cancellationToken) => {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            await using HtmlBrowserSession session = await OpenSessionForNavigationAsync(launch, cancellationToken).ConfigureAwait(false);
            IPage page = session.Page;
            // The navigation Playwright returns is the first one; a solved challenge reloads, so keep the last document.
            IResponse? document = null;
            void Track(object? sender, IResponse response) {
                if (response.Request.IsNavigationRequest && response.Frame == page.MainFrame) document = response;
            }
            page.Response += Track;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                await page.GotoAsync(uri.AbsoluteUri, new PageGotoOptions {
                    Timeout = launch.Timeout,
                    WaitUntil = ToWaitUntilState(launch.LoadState)
                }).ConfigureAwait(false);
            } finally {
                page.Response -= Track;
            }
            string html = await page.ContentAsync().ConfigureAwait(false);
            using HttpResponseMessage probe = new((HttpStatusCode)(document?.Status ?? 200)) {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            if (document != null) {
                foreach (var header in document.Headers) probe.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HtmlHttpFailure? failure = HtmlResponseClassifier.ClassifyRendered(probe, html);
            if (failure != null) throw new HtmlHttpException(HtmlResponseClassifier.Describe(failure, null, "text/html"), failure);
            bool navigated = Uri.TryCreate(page.Url, UriKind.Absolute, out Uri? final) && final.Scheme is "http" or "https";
            return new HtmlRenderedPage(html, navigated ? final : null);
        };
    }
}
