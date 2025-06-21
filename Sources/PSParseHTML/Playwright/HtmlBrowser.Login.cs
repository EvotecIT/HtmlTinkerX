using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper methods for detecting login forms on a page.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Attempts to locate a login form on the given page and return selectors for
    /// its common fields.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <returns>Selectors for the username, password and submit elements or <c>null</c> if none found.</returns>
    public static async Task<HtmlFormLogin?> DetectLoginFormAsync(IPage page, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        const string selectorScript = @"(el) => {
            const esc = CSS && CSS.escape ? CSS.escape : (s) => s;
            let sel = el.tagName.toLowerCase();
            if (el.id) return sel + '#' + esc(el.id);
            const name = el.getAttribute('name');
            if (name) return `${sel}[name='${name.replace(/'/g, '\\'')}']`;
            const cls = el.className;
            if (cls) return sel + '.' + cls.trim().split(/\\s+/).map(esc).join('.');
            return sel;
        }";

        foreach (var form in await page.QuerySelectorAllAsync("form")) {
            cancellationToken.ThrowIfCancellationRequested();
            var password = await form.QuerySelectorAsync("input[type=password]");
            if (password == null) {
                continue;
            }

            var username = await form.QuerySelectorAsync("input[type=email],input[type=text],input:not([type])");
            var submit = await form.QuerySelectorAsync("input[type=submit],button[type=submit],button:not([type])");

            string userSel = string.Empty;
            string passSel = string.Empty;
            string submitSel = string.Empty;

            if (username != null) {
                userSel = await username.EvaluateAsync<string>(selectorScript);
            }

            passSel = await password.EvaluateAsync<string>(selectorScript);

            if (submit != null) {
                submitSel = await submit.EvaluateAsync<string>(selectorScript);
            }

            return new HtmlFormLogin {
                LoginUrl = page.Url,
                UsernameSelector = userSel,
                PasswordSelector = passSel,
                SubmitSelector = submitSel
            };
        }

        return null;
    }

    /// <summary>
    /// Detects a login form using an existing browser session.
    /// </summary>
    public static Task<HtmlFormLogin?> DetectLoginFormAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default)
        => DetectLoginFormAsync(session.Page, cancellationToken);
}
