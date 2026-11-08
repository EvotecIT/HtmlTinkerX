using Microsoft.Playwright;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static readonly TimeSpan ResumePollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Creates an <see cref="HtmlFetchPolicy.OnBlocked"/> decision that pauses a blocked request for a person: it opens
    /// the page in a browser, waits while the person completes whatever the site asks, then copies the site's browser
    /// cookies into <paramref name="cookies"/> and returns <see cref="HtmlBlockedAction.Resumed"/> so the request is
    /// sent again with them. Nothing is solved automatically.
    /// </summary>
    /// <remarks>
    /// <paramref name="cookies"/> must be the container the handler's transport sends, for example the one given to
    /// <see cref="HtmlProxyRotator.Create"/>.
    /// The retry needs <see cref="HtmlFetchPolicy.RetryCount"/> of one or more. When the person does not finish within
    /// <see cref="HtmlBrowserResumeOptions.Timeout"/>, or closes the page, the request fails as blocked. Only cookies
    /// for the blocked address are copied; with the person's own Chrome these are that browser's cookies for the site.
    /// </remarks>
    /// <param name="cookies">Cookie container of the HTTP transport that receives the browser's cookies.</param>
    /// <param name="options">Browser, timeout and wait; a headed Chromium and a five-minute wait when null.</param>
    public static Func<Uri, HtmlHttpFailure, CancellationToken, Task<HtmlBlockedAction>> CreateBlockedResume(CookieContainer cookies, HtmlBrowserResumeOptions? options = null) {
        if (cookies == null) throw new ArgumentNullException(nameof(cookies));
        HtmlBrowserResumeOptions resume = options ?? new HtmlBrowserResumeOptions();
        if (resume.Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options), resume.Timeout, "The resume timeout must be positive.");
        HtmlBrowserLaunchOptions launch = resume.Browser ?? new HtmlBrowserLaunchOptions { Headless = false };
        return async (uri, failure, cancellationToken) => {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(resume.Timeout);
            try {
                await using HtmlBrowserSession session = await OpenSessionForNavigationAsync(launch, timeout.Token).ConfigureAwait(false);
                IPage page = session.Page;
                IResponse? document = null;
                void Track(object? sender, IResponse response) {
                    if (response.Request.IsNavigationRequest && response.Frame == page.MainFrame) document = response;
                }
                page.Response += Track;
                bool finished;
                try {
                    await page.GotoAsync(uri.AbsoluteUri, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 0 })
                        .WaitWithCancellationAsync(timeout.Token).ConfigureAwait(false);
                    finished = resume.WaitForPerson != null
                        ? await resume.WaitForPerson(session, timeout.Token).ConfigureAwait(false)
                        : await WaitUntilUnblockedAsync(page, () => document, timeout.Token).ConfigureAwait(false);
                } finally {
                    page.Response -= Track;
                }
                if (!finished) return HtmlBlockedAction.Fail;
                foreach (BrowserContextCookiesResult cookie in await session.Context.CookiesAsync(new[] { uri.AbsoluteUri }).ConfigureAwait(false)) {
                    cookies.Add(ToCookie(cookie));
                }
                return HtmlBlockedAction.Resumed;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                return HtmlBlockedAction.Fail;
            }
        };
    }

    /// <summary>Polls the page until its last document is no longer a challenge or block page; false once the person closes it.</summary>
    private static async Task<bool> WaitUntilUnblockedAsync(IPage page, Func<IResponse?> document, CancellationToken cancellationToken) {
        while (true) {
            if (page.IsClosed) return false;
            try {
                string html = await page.ContentAsync().ConfigureAwait(false);
                IResponse? last = document();
                using HttpResponseMessage probe = new((HttpStatusCode)(last?.Status ?? 200)) {
                    Content = new StringContent(html, Encoding.UTF8, "text/html")
                };
                if (last != null) {
                    foreach (var header in last.Headers) probe.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                if (HtmlResponseClassifier.ClassifyRendered(probe, html) == null) return true;
            } catch (PlaywrightException) when (!page.IsClosed) {
                // The page is between documents while the site's check navigates; look again shortly.
            } catch (PlaywrightException) {
                return false;
            }
            await Task.Delay(ResumePollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static System.Net.Cookie ToCookie(BrowserContextCookiesResult cookie) => new(cookie.Name, cookie.Value, cookie.Path, cookie.Domain) {
        HttpOnly = cookie.HttpOnly,
        Secure = cookie.Secure,
        // Playwright reports -1 for a session cookie, which lives as long as the container.
        Expires = cookie.Expires > 0 ? DateTimeOffset.FromUnixTimeMilliseconds((long)(cookie.Expires * 1000)).UtcDateTime : DateTime.MinValue
    };
}
