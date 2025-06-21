using Microsoft.Playwright;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Retrieves cookies from the browser context.
    /// </summary>
    public static async Task<List<HtmlCookie>> GetCookiesAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BrowserContextCookiesResult> cookies = await session.Context.CookiesAsync();
        List<HtmlCookie> result = new();
        foreach (BrowserContextCookiesResult c in cookies) {
            result.Add(new HtmlCookie {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.Expires,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite
            });
        }
        return result;
    }

    /// <summary>
    /// Adds cookies to the browser context.
    /// </summary>
    public static Task SetCookiesAsync(HtmlBrowserSession session, IEnumerable<HtmlCookie> cookies, CancellationToken cancellationToken = default) {
        List<Cookie> list = new();
        foreach (HtmlCookie c in cookies) {
            Cookie nc = new() {
                Name = c.Name,
                Value = c.Value,
                Url = c.Url,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.Expires,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite
            };
            list.Add(nc);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return list.Count == 0
            ? Task.CompletedTask
            : session.Context.AddCookiesAsync(list);
    }
}
