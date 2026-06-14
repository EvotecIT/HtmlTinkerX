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

    private static IEnumerable<string> NormalizeInteractionValues(IEnumerable<string>? values) =>
        values == null
            ? Array.Empty<string>()
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

    private static async Task<bool> TryClickSelectorAsync(IPage page, string selector, int interactionDelayMs, int timeout, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.Locator(selector).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0) {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = Math.Min(timeout, 3000)
            }).ConfigureAwait(false);
            await WaitAfterInteractionAsync(page, interactionDelayMs).ConfigureAwait(false);
            return true;
        } catch {
            return false;
        }
    }

    private static async Task<bool> TryClickTextAsync(IPage page, string text, int interactionDelayMs, int timeout, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.GetByText(text).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0) {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = Math.Min(timeout, 3000)
            }).ConfigureAwait(false);
            await WaitAfterInteractionAsync(page, interactionDelayMs).ConfigureAwait(false);
            return true;
        } catch {
            return false;
        }
    }

    private static async Task WaitAfterInteractionAsync(IPage page, int interactionDelayMs) {
        if (interactionDelayMs > 0) {
            await page.WaitForTimeoutAsync(interactionDelayMs).ConfigureAwait(false);
        }
    }
}
