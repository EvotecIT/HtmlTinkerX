using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for detecting login forms in an HTML page.
/// </summary>
public static partial class HtmlBrowser {

    /// <summary>
    /// Attempts to detect a login form on the specified page and returns the CSS selectors for the username,
    /// password and submit fields.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A populated <see cref="HtmlFormLogin"/> if a login form was found; otherwise <c>null</c>.</returns>
    public static async Task<HtmlFormLogin?> DetectLoginFormAsync(IPage page, CancellationToken cancellationToken = default) {
        if (page == null) {
            throw new System.ArgumentNullException(nameof(page));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string html = await page.ContentAsync().ConfigureAwait(false);
        return HtmlLoginParser.Detect(html, page.Url);
    }

    /// <summary>
    /// Detects a login form using an existing browser session.
    /// </summary>
    /// <param name="session">Browser session to inspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A populated <see cref="HtmlFormLogin"/> if a login form was found; otherwise <c>null</c>.</returns>
    public static Task<HtmlFormLogin?> DetectLoginFormAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default)
        => DetectLoginFormAsync(session.Page, cancellationToken);
}