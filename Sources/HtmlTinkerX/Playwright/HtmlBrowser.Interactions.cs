using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for applying simple rendered-page interactions before extraction.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Applies dismiss and click interactions to a rendered page and returns descriptions of successful actions.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="clickSelectors">CSS selectors to click after dismissals.</param>
    /// <param name="clickTexts">Visible text values to click after dismissals.</param>
    /// <param name="dismissSelectors">CSS selectors to click once before normal interactions.</param>
    /// <param name="dismissTexts">Visible text values to click once before normal interactions.</param>
    /// <param name="interactionDelayMs">Delay after each successful interaction.</param>
    /// <param name="interactionRepeatCount">Number of times to repeat normal click interactions.</param>
    /// <param name="timeout">Interaction timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptions of interactions that were applied.</returns>
    public static async Task<IReadOnlyList<string>> ApplyPageInteractionsAsync(
        IPage page,
        IEnumerable<string>? clickSelectors = null,
        IEnumerable<string>? clickTexts = null,
        IEnumerable<string>? dismissSelectors = null,
        IEnumerable<string>? dismissTexts = null,
        int interactionDelayMs = 300,
        int interactionRepeatCount = 1,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }

        if (interactionDelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(interactionDelayMs), "InteractionDelayMs must be zero or greater.");
        }

        if (interactionRepeatCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(interactionRepeatCount), "InteractionRepeatCount must be greater than zero.");
        }

        List<string> applied = new();
        foreach (string text in NormalizeInteractionValues(dismissTexts)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryClickTextAsync(page, text, interactionDelayMs, timeout, cancellationToken).ConfigureAwait(false)) {
                applied.Add($"Dismissed text: {text}");
            }
        }

        foreach (string selector in NormalizeInteractionValues(dismissSelectors)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryClickSelectorAsync(page, selector, interactionDelayMs, timeout, cancellationToken).ConfigureAwait(false)) {
                applied.Add($"Dismissed: {selector}");
            }
        }

        for (int i = 0; i < interactionRepeatCount; i++) {
            foreach (string text in NormalizeInteractionValues(clickTexts)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (await TryClickTextAsync(page, text, interactionDelayMs, timeout, cancellationToken).ConfigureAwait(false)) {
                    applied.Add(interactionRepeatCount > 1 ? $"Clicked text [{i + 1}]: {text}" : $"Clicked text: {text}");
                }
            }

            foreach (string selector in NormalizeInteractionValues(clickSelectors)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (await TryClickSelectorAsync(page, selector, interactionDelayMs, timeout, cancellationToken).ConfigureAwait(false)) {
                    applied.Add(interactionRepeatCount > 1 ? $"Clicked [{i + 1}]: {selector}" : $"Clicked: {selector}");
                }
            }
        }

        return applied;
    }

    /// <summary>
    /// Attempts to click the first visible element matching text and returns false when the text is absent, hidden, or times out.
    /// </summary>
    public static Task<bool> TryClickTextAsync(
        HtmlBrowserSession session,
        string text,
        bool exact = false,
        string? regex = null,
        int timeout = 10000,
        CancellationToken cancellationToken = default) =>
        TryClickTextAsync(session, text, exact, regex, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Attempts to click a visible element matching text and returns false when the text is absent, hidden, or times out.
    /// </summary>
    public static async Task<bool> TryClickTextAsync(
        HtmlBrowserSession session,
        string text,
        bool exact,
        string? regex,
        int timeout,
        CancellationToken cancellationToken,
        int? nth) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        try {
            ILocator matches = !string.IsNullOrEmpty(regex)
                ? session.Page.GetByText(new System.Text.RegularExpressions.Regex(regex))
                : exact
                    ? session.Page.GetByText(text, new PageGetByTextOptions { Exact = true })
                    : session.Page.GetByText(text);
            ILocator locator = nth.HasValue ? matches.Nth(nth.Value) : matches.First;

            cancellationToken.ThrowIfCancellationRequested();
            await locator.WaitForAsync(new LocatorWaitForOptions {
                State = WaitForSelectorState.Visible,
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await RecordRecipeStepAsync(session, new HtmlBrowserRecipeStep {
                Action = HtmlBrowserRecipeAction.ClickText,
                Text = text,
                Exact = exact,
                Regex = regex,
                Nth = nth,
                Timeout = timeout,
                ContinueOnError = true
            }, cancellationToken).ConfigureAwait(false);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (PlaywrightException) {
            return false;
        } catch (TimeoutException) {
            return false;
        }
    }

    private static IEnumerable<string> NormalizeInteractionValues(IEnumerable<string>? values) =>
        values == null
            ? Array.Empty<string>()
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

    private static async Task<bool> TryClickSelectorAsync(IPage page, string selector, int interactionDelayMs, int timeout, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.Locator(selector).First;
            cancellationToken.ThrowIfCancellationRequested();
            await locator.WaitForAsync(new LocatorWaitForOptions {
                State = WaitForSelectorState.Visible,
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await WaitAfterInteractionAsync(interactionDelayMs, cancellationToken).ConfigureAwait(false);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            return false;
        }
    }

    private static async Task<bool> TryClickTextAsync(IPage page, string text, int interactionDelayMs, int timeout, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.GetByText(text).First;
            cancellationToken.ThrowIfCancellationRequested();
            await locator.WaitForAsync(new LocatorWaitForOptions {
                State = WaitForSelectorState.Visible,
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = timeout
            }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await WaitAfterInteractionAsync(interactionDelayMs, cancellationToken).ConfigureAwait(false);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            return false;
        }
    }

    private static async Task WaitAfterInteractionAsync(int interactionDelayMs, CancellationToken cancellationToken) {
        if (interactionDelayMs > 0) {
            await Task.Delay(interactionDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitWithCancellationAsync(this Task task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) {
            await task.ConfigureAwait(false);
            return;
        }

        TaskCompletionSource<bool> cancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellation);
        Task completed = await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false);
        if (completed != task) {
            _ = task.ContinueWith(static completedTask => _ = completedTask.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }

        await task.ConfigureAwait(false);
    }
}
