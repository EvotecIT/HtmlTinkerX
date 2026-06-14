using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for browser navigation and rendered-page readiness.
/// </summary>
public static partial class HtmlBrowser {
    private static async Task NavigateAsync(
        IPage page,
        string url,
        HtmlFormLogin? formLogin,
        string? username,
        string? password,
        HtmlBrowserLoadState loadState,
        int timeout,
        CancellationToken cancellationToken) {
        if (formLogin != null) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.GotoAsync(formLogin.LoginUrl, new PageGotoOptions {
                Timeout = timeout,
                WaitUntil = ToWaitUntilState(loadState)
            }).ConfigureAwait(false);
            if (username != null) {
                await page.FillAsync(formLogin.UsernameSelector, username, new PageFillOptions { Timeout = timeout }).ConfigureAwait(false);
            }
            if (password != null) {
                await page.FillAsync(formLogin.PasswordSelector, password, new PageFillOptions { Timeout = timeout }).ConfigureAwait(false);
            }
            await page.ClickAsync(formLogin.SubmitSelector, new PageClickOptions { Timeout = timeout }).ConfigureAwait(false);
            await WaitForLoadStateAsync(page, HtmlBrowserLoadState.NetworkIdle, timeout, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await page.GotoAsync(url, new PageGotoOptions {
            Timeout = timeout,
            WaitUntil = ToWaitUntilState(loadState)
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for optional rendered-page readiness conditions before content extraction.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="waitForSelector">Optional CSS selector that must appear before extraction continues.</param>
    /// <param name="waitForFunction">Optional JavaScript predicate that must evaluate truthy before extraction continues.</param>
    /// <param name="clickSelectors">CSS selectors to click before extraction.</param>
    /// <param name="clickTexts">Visible text values to click before extraction.</param>
    /// <param name="dismissSelectors">CSS selectors to dismiss before normal click interactions.</param>
    /// <param name="dismissTexts">Visible text values to dismiss before normal click interactions.</param>
    /// <param name="interactionDelayMs">Delay after each successful rendered interaction.</param>
    /// <param name="interactionRepeatCount">Number of times to repeat normal click interactions.</param>
    /// <param name="waitAfterLoadMs">Optional delay after page load or selector readiness.</param>
    /// <param name="autoScroll">Scrolls to the bottom and back to the top to trigger lazy-loaded content.</param>
    /// <param name="autoScrollSteps">Number of incremental scroll attempts when <paramref name="autoScroll"/> is enabled.</param>
    /// <param name="autoScrollDelayMs">Delay after each scroll step.</param>
    /// <param name="timeout">Selector wait timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<IReadOnlyList<string>> PreparePageForContentAsync(
        IPage page,
        string? waitForSelector = null,
        string? waitForFunction = null,
        IEnumerable<string>? clickSelectors = null,
        IEnumerable<string>? clickTexts = null,
        IEnumerable<string>? dismissSelectors = null,
        IEnumerable<string>? dismissTexts = null,
        int interactionDelayMs = 300,
        int interactionRepeatCount = 1,
        int waitAfterLoadMs = 0,
        bool autoScroll = false,
        int autoScrollSteps = 3,
        int autoScrollDelayMs = 400,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }

        if (waitAfterLoadMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(waitAfterLoadMs), "WaitAfterLoadMs must be zero or greater.");
        }

        if (autoScrollSteps <= 0) {
            throw new ArgumentOutOfRangeException(nameof(autoScrollSteps), "AutoScrollSteps must be greater than zero.");
        }

        if (autoScrollDelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(autoScrollDelayMs), "AutoScrollDelayMs must be zero or greater.");
        }

        bool hasExplicitInteractions =
            HasAny(clickSelectors) ||
            HasAny(clickTexts) ||
            HasAny(dismissSelectors) ||
            HasAny(dismissTexts);

        bool hasPageChangingActions = hasExplicitInteractions || autoScroll;

        if (!hasPageChangingActions) {
            await WaitForExtractionReadinessAsync(page, waitForSelector, waitForFunction, timeout, cancellationToken).ConfigureAwait(false);
            await WaitAfterLoadAsync(page, waitAfterLoadMs, cancellationToken).ConfigureAwait(false);
        }

        if (hasExplicitInteractions) {
            await WaitAfterLoadAsync(page, waitAfterLoadMs, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<string> appliedInteractions = await ApplyPageInteractionsAsync(
            page,
            clickSelectors,
            clickTexts,
            dismissSelectors,
            dismissTexts,
            interactionDelayMs,
            interactionRepeatCount,
            timeout,
            cancellationToken).ConfigureAwait(false);

        if (autoScroll && !hasExplicitInteractions) {
            await WaitAfterLoadAsync(page, waitAfterLoadMs, cancellationToken).ConfigureAwait(false);
        }

        if (autoScroll) {
            for (int i = 0; i < autoScrollSteps; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)").ConfigureAwait(false);
                if (autoScrollDelayMs > 0) {
                    await page.WaitForTimeoutAsync(autoScrollDelayMs).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync("() => window.scrollTo(0, 0)").ConfigureAwait(false);
        }

        if (hasPageChangingActions) {
            await WaitForExtractionReadinessAsync(page, waitForSelector, waitForFunction, timeout, cancellationToken).ConfigureAwait(false);
            await WaitAfterLoadAsync(page, waitAfterLoadMs, cancellationToken).ConfigureAwait(false);
        }

        return appliedInteractions;
    }

    private static async Task WaitForExtractionReadinessAsync(IPage page, string? waitForSelector, string? waitForFunction, int timeout, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(waitForSelector)) {
            await page.WaitForSelectorAsync(waitForSelector!, new PageWaitForSelectorOptions {
                Timeout = timeout
            }).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(waitForFunction)) {
            await page.WaitForFunctionAsync(waitForFunction!, null, new PageWaitForFunctionOptions {
                Timeout = timeout
            }).ConfigureAwait(false);
        }
    }

    private static async Task WaitForFunctionReadinessAsync(IPage page, string? waitForFunction, int timeout, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(waitForFunction)) {
            await page.WaitForFunctionAsync(waitForFunction!, null, new PageWaitForFunctionOptions {
                Timeout = timeout
            }).ConfigureAwait(false);
        }
    }

    private static async Task WaitAfterLoadAsync(IPage page, int waitAfterLoadMs, CancellationToken cancellationToken) {
        if (waitAfterLoadMs > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(waitAfterLoadMs).ConfigureAwait(false);
        }
    }

    private static bool HasAny(IEnumerable<string>? values) {
        if (values == null) {
            return false;
        }

        foreach (string? value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Navigates the specified session to a new URL and waits for the network to be idle.
    /// </summary>
    public static async Task NavigateAsync(HtmlBrowserSession session, string url, int timeout = 10000, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await session.Page.GotoAsync(url, new PageGotoOptions {
            Timeout = timeout,
            WaitUntil = WaitUntilState.NetworkIdle
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Navigates the specified session to a new URL and waits for the requested load state.
    /// </summary>
    public static async Task NavigateAsync(HtmlBrowserSession session, string url, HtmlBrowserLoadState loadState, int timeout = 10000, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await session.Page.GotoAsync(url, new PageGotoOptions {
            Timeout = timeout,
            WaitUntil = ToWaitUntilState(loadState)
        }).ConfigureAwait(false);
    }

    private static WaitUntilState ToWaitUntilState(HtmlBrowserLoadState loadState) => loadState switch {
        HtmlBrowserLoadState.Commit => WaitUntilState.Commit,
        HtmlBrowserLoadState.DomContentLoaded => WaitUntilState.DOMContentLoaded,
        HtmlBrowserLoadState.Load => WaitUntilState.Load,
        _ => WaitUntilState.NetworkIdle
    };

    private static async Task WaitForLoadStateAsync(IPage page, HtmlBrowserLoadState loadState, int timeout, CancellationToken cancellationToken) {
        if (loadState == HtmlBrowserLoadState.Commit) {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        LoadState playwrightLoadState = loadState switch {
            HtmlBrowserLoadState.DomContentLoaded => LoadState.DOMContentLoaded,
            HtmlBrowserLoadState.Load => LoadState.Load,
            _ => LoadState.NetworkIdle
        };
        await page.WaitForLoadStateAsync(playwrightLoadState, new PageWaitForLoadStateOptions {
            Timeout = timeout
        }).ConfigureAwait(false);
    }
}
