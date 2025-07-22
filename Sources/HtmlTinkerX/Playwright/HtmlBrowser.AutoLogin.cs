using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for automatically filling detected login forms.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Detects a login form on the page and fills it with the provided credentials.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="username">Username value to enter.</param>
    /// <param name="password">Password value to enter.</param>
    /// <param name="timeout">Element wait timeout in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><c>true</c> if a login form was found and submitted; otherwise <c>false</c>.</returns>
    public static async Task<bool> AutoLoginAsync(
        IPage page,
        string username,
        string password,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        if (page is null) {
            throw new System.ArgumentNullException(nameof(page));
        }

        HtmlFormLogin? login = await DetectLoginFormAsync(page, cancellationToken).ConfigureAwait(false);
        if (login == null) {
            return false;
        }

        if (!string.IsNullOrEmpty(login.UsernameSelector)) {
            await FillInputAsync(page, login.UsernameSelector, username, timeout, cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrEmpty(login.PasswordSelector)) {
            await FillInputAsync(page, login.PasswordSelector, password, timeout, cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrEmpty(login.SubmitSelector)) {
            await MouseClickAsync(page, login.SubmitSelector, MouseButton.Left, 1, null, timeout, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Detects a login form on the page using an existing session and fills it with credentials.
    /// </summary>
    /// <param name="session">Browser session to operate on.</param>
    /// <param name="username">Username value to enter.</param>
    /// <param name="password">Password value to enter.</param>
    /// <param name="timeout">Element wait timeout in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><c>true</c> if a login form was found and submitted; otherwise <c>false</c>.</returns>
    public static Task<bool> AutoLoginAsync(
        HtmlBrowserSession session,
        string username,
        string password,
        int timeout = 10000,
        CancellationToken cancellationToken = default) =>
        AutoLoginAsync(session.Page, username, password, timeout, cancellationToken);
}
