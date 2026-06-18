using Microsoft.Playwright;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Retrieves cookies from the browser context.
    /// </summary>
    /// <param name="session">Session from which cookies will be read.</param>
    /// <param name="domains">Optional domain filter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task<List<HtmlCookie>> GetCookiesAsync(HtmlBrowserSession session, IEnumerable<string>? domains = null, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BrowserContextCookiesResult> cookies = await session.Context.CookiesAsync();
        List<HtmlCookie> result = new();
        foreach (BrowserContextCookiesResult c in cookies) {
            result.Add(new HtmlCookie {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.Expires > 0 ? (long)c.Expires : (long?)null,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite
            });
        }
        if (domains is { }) {
            HashSet<string> filter = new(domains, System.StringComparer.OrdinalIgnoreCase);
            result.RemoveAll(c => c.Domain is null || !filter.Contains(c.Domain));
        }
        return result;
    }

    /// <summary>
    /// Retrieves cookies from the browser context.
    /// </summary>
    /// <param name="session">Session from which cookies will be read.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static Task<List<HtmlCookie>> GetCookiesAsync(HtmlBrowserSession session, CancellationToken cancellationToken) =>
        GetCookiesAsync(session, null, cancellationToken);

    /// <summary>
    /// Adds cookies to the browser context.
    /// </summary>
    /// <param name="session">Session to which cookies will be added.</param>
    /// <param name="cookies">Collection of cookies to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task SetCookiesAsync(HtmlBrowserSession session, IEnumerable<HtmlCookie> cookies, CancellationToken cancellationToken = default) {
        List<Cookie> list = new();
        foreach (HtmlCookie c in cookies) {
            Cookie nc = new() {
                Name = c.Name,
                Value = c.Value,
                Url = c.Url,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.Expires.HasValue ? (float)c.Expires.Value : null,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite
            };
            list.Add(nc);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await session.Context.AddCookiesAsync(list).ConfigureAwait(false);
    }
}
