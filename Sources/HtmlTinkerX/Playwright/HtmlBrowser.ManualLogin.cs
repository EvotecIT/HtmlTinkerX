using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for manual and enterprise login flows.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Waits for a selector that indicates a manual login flow has completed.
    /// </summary>
    /// <param name="session">Browser session to inspect.</param>
    /// <param name="successSelector">CSS selector that appears after successful login.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a selector was supplied and observed; otherwise <c>false</c>.</returns>
    public static async Task<bool> WaitForManualLoginAsync(
        HtmlBrowserSession session,
        string? successSelector,
        int timeout = 120000,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(successSelector)) {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await session.Page.WaitForSelectorAsync(successSelector!, new PageWaitForSelectorOptions {
            Timeout = timeout,
            State = WaitForSelectorState.Visible
        }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
