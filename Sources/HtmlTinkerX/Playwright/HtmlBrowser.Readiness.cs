using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for browser readiness waits.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Waits until a browser session satisfies the requested readiness conditions.
    /// </summary>
    /// <param name="session">Browser session to wait on.</param>
    /// <param name="options">Readiness options. When omitted, waits for network idle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The same session after readiness conditions complete.</returns>
    public static async Task<HtmlBrowserSession> WaitUntilReadyAsync(
        HtmlBrowserSession session,
        HtmlBrowserReadinessOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        options ??= new HtmlBrowserReadinessOptions();
        ValidateReadinessOptions(options);

        if (!options.SkipLoadState) {
            await WaitForLoadStateAsync(session.Page, options.LoadState, options.Timeout, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(options.Selector)) {
            cancellationToken.ThrowIfCancellationRequested();
            await session.Page.WaitForSelectorAsync(options.Selector!, new PageWaitForSelectorOptions {
                Timeout = options.Timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(options.Function)) {
            cancellationToken.ThrowIfCancellationRequested();
            await session.Page.WaitForFunctionAsync(options.Function!, null, new PageWaitForFunctionOptions {
                Timeout = options.Timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        if (options.Stable) {
            await WaitUntilStableAsync(
                session,
                options.StableMilliseconds,
                options.PollMilliseconds,
                options.Timeout,
                cancellationToken).ConfigureAwait(false);
        }

        await RecordRecipeStepAsync(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.WaitReady,
            LoadState = options.LoadState,
            NoLoadState = options.SkipLoadState,
            Selector = options.Selector,
            Script = options.Function,
            Stable = options.Stable,
            StableMilliseconds = options.StableMilliseconds,
            PollMilliseconds = options.PollMilliseconds,
            Timeout = options.Timeout
        }, cancellationToken).ConfigureAwait(false);

        return session;
    }

    private static void ValidateReadinessOptions(HtmlBrowserReadinessOptions options) {
        if (options.Timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.Timeout), "Timeout must be zero or greater.");
        }

        if (options.StableMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.StableMilliseconds), "StableMilliseconds must be zero or greater.");
        }

        if (options.PollMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.PollMilliseconds), "PollMilliseconds must be greater than zero.");
        }
    }
}
