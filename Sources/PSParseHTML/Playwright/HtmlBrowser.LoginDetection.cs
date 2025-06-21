using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML;

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
        string script = """
            () => {
                const esc = (CSS && CSS.escape) ? CSS.escape : (s => s);
                const toSelector = el => {
                    if (!el) return '';
                    let sel = el.tagName.toLowerCase();
                    if (el.id) return sel + '#' + esc(el.id);
                    const name = el.getAttribute('name');
                    if (name) return `${sel}[name='${name.replace(/'/g, "\\'")}]`;
                    const cls = el.className;
                    if (cls) return sel + '.' + cls.trim().split(/\s+/).map(esc).join('.');
                    return sel;
                };
                const pwd = document.querySelector('input[type="password"]');
                if (!pwd) return null;
                const form = pwd.closest('form');
                if (!form) return null;
                const user = form.querySelector('input[type="text"],input[type="email"],input[name*="user" i],input[name*="login" i]');
                const submit = form.querySelector('input[type="submit"],button[type="submit"],button:not([type])');
                return { username: toSelector(user), password: toSelector(pwd), submit: toSelector(submit) };
            }
        """;
        System.Collections.Generic.Dictionary<string, string?>? selectors = await page.EvaluateAsync<System.Collections.Generic.Dictionary<string, string?>>(script).ConfigureAwait(false);

        if (selectors == null || string.IsNullOrEmpty(selectors["password"])) {
            return null;
        }

        return new HtmlFormLogin {
            LoginUrl = page.Url,
            UsernameSelector = selectors["username"] ?? string.Empty,
            PasswordSelector = selectors["password"] ?? string.Empty,
            SubmitSelector = selectors["submit"] ?? string.Empty
        };
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
