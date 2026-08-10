using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Shared visual masking helpers for screenshots, PDFs, and other browser evidence artifacts.
/// </summary>
public static partial class HtmlBrowser {
    private static readonly string[] DefaultSensitiveVisualMaskSelectors = {
        "input[type='password']",
        "input[name*='password' i]",
        "input[id*='password' i]",
        "input[name*='passwd' i]",
        "input[id*='passwd' i]",
        "input[name*='pwd' i]",
        "input[id*='pwd' i]",
        "input[name*='token' i]",
        "input[id*='token' i]",
        "input[name*='secret' i]",
        "input[id*='secret' i]",
        "input[name*='credential' i]",
        "input[id*='credential' i]",
        "input[name*='samlresponse' i]",
        "input[name*='samlrequest' i]",
        "input[name*='relaystate' i]",
        "input[name*='otp' i]",
        "input[id*='otp' i]",
        "input[name*='mfa' i]",
        "input[id*='mfa' i]",
        "input[name*='pin' i]",
        "input[id*='pin' i]",
        "textarea[name*='token' i]",
        "textarea[id*='token' i]",
        "textarea[name*='secret' i]",
        "textarea[id*='secret' i]",
        "textarea[name*='samlresponse' i]",
        "textarea[name*='samlrequest' i]",
        "textarea[name*='relaystate' i]",
        "textarea[name*='otp' i]",
        "textarea[id*='otp' i]",
        "textarea[name*='mfa' i]",
        "textarea[id*='mfa' i]",
        "textarea[name*='pin' i]",
        "textarea[id*='pin' i]"
    };
    private const string ApplyVisualMaskScript =
        @"({ selectors, color }) => {
            const marker = 'data-htmltinkerx-visual-mask';
            const previousStyle = 'data-htmltinkerx-visual-mask-style';
            const hadStyle = 'data-htmltinkerx-visual-mask-had-style';
            for (const selector of selectors || []) {
                if (!selector || !selector.trim()) continue;
                let elements = [];
                try { elements = Array.from(document.querySelectorAll(selector)); } catch { continue; }
                for (const element of elements) {
                    if (!(element instanceof HTMLElement)) continue;
                    if (!element.hasAttribute(marker)) {
                        const currentStyle = element.getAttribute('style');
                        element.setAttribute(previousStyle, currentStyle || '');
                        element.setAttribute(hadStyle, currentStyle === null ? 'false' : 'true');
                        element.setAttribute(marker, 'true');
                    }
                    element.style.setProperty('background-color', color, 'important');
                    element.style.setProperty('border-color', color, 'important');
                    element.style.setProperty('box-shadow', 'none', 'important');
                    element.style.setProperty('caret-color', 'transparent', 'important');
                    element.style.setProperty('color', 'transparent', 'important');
                    element.style.setProperty('filter', 'none', 'important');
                    element.style.setProperty('outline-color', color, 'important');
                    element.style.setProperty('text-shadow', 'none', 'important');
                }
            }
        }";

    internal static async Task<T> ExecuteWithTemporaryVisualMaskAsync<T>(
        IPage page,
        bool maskSensitiveElements,
        IEnumerable<string>? maskSelectors,
        string? maskColor,
        Func<Task<T>> action,
        CancellationToken cancellationToken) {
        string[] selectors = CreateVisualMaskSelectors(maskSensitiveElements, maskSelectors);
        if (selectors.Length == 0) {
            return await action().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IFrame> maskedChildFrames = await ApplyTemporaryVisualMaskAsync(page, selectors, maskColor, cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            return await action().ConfigureAwait(false);
        } finally {
            try {
                await RemoveTemporaryVisualMaskAsync(page, maskedChildFrames).ConfigureAwait(false);
            } catch (PlaywrightException) when (cancellationToken.IsCancellationRequested || page.IsClosed) {
                // Cancellation closes the page to interrupt Playwright. Preserve the original
                // cancellation instead of replacing it with cleanup failure from the closed page.
            }
        }
    }

    private static string[] CreateVisualMaskSelectors(bool maskSensitiveElements, IEnumerable<string>? maskSelectors) {
        List<string> selectors = new();
        if (maskSensitiveElements) {
            selectors.AddRange(DefaultSensitiveVisualMaskSelectors);
        }

        if (maskSelectors != null) {
            foreach (string selector in maskSelectors) {
                if (!string.IsNullOrWhiteSpace(selector)) {
                    selectors.Add(selector);
                }
            }
        }

        return selectors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static async Task<IReadOnlyList<IFrame>> ApplyTemporaryVisualMaskAsync(IPage page, string[] selectors, string? maskColor, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string color = string.IsNullOrWhiteSpace(maskColor) ? "#000000" : maskColor!;
        object arguments = new {
            selectors,
            color
        };
        await page.EvaluateAsync(ApplyVisualMaskScript, arguments).ConfigureAwait(false);

        List<IFrame> maskedChildFrames = new();
        IReadOnlyList<IFrame>? frames = page.Frames;
        if (frames == null) return maskedChildFrames;
        try {
            foreach (IFrame frame in frames) {
                if (ReferenceEquals(frame, page.MainFrame)) continue;
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    await frame.EvaluateAsync(ApplyVisualMaskScript, arguments).ConfigureAwait(false);
                    maskedChildFrames.Add(frame);
                } catch (PlaywrightException) when (frame.IsDetached) {
                    // Detached frames are no longer part of the artifact.
                }
            }
        } catch {
            try { await RemoveTemporaryVisualMaskAsync(page, maskedChildFrames).ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
        return maskedChildFrames;
    }

    private static async Task RemoveTemporaryVisualMaskAsync(IPage page, IReadOnlyList<IFrame> maskedChildFrames) {
        const string restoreScript =
            @"() => {
                const marker = 'data-htmltinkerx-visual-mask';
                const previousStyle = 'data-htmltinkerx-visual-mask-style';
                const hadStyle = 'data-htmltinkerx-visual-mask-had-style';
                for (const element of Array.from(document.querySelectorAll('[' + marker + ']'))) {
                    if (!(element instanceof HTMLElement)) {
                        continue;
                    }

                    const originalStyle = element.getAttribute(previousStyle) || '';
                    const originalHadStyle = element.getAttribute(hadStyle) === 'true';
                    if (originalHadStyle) {
                        element.setAttribute('style', originalStyle);
                    } else {
                        element.removeAttribute('style');
                    }

                    element.removeAttribute(marker);
                    element.removeAttribute(previousStyle);
                    element.removeAttribute(hadStyle);
                }
            }";
        await page.EvaluateAsync(restoreScript).ConfigureAwait(false);
        foreach (IFrame frame in maskedChildFrames) {
            try {
                await frame.EvaluateAsync(restoreScript).ConfigureAwait(false);
            } catch (PlaywrightException) when (frame.IsDetached) {
                // Detached frames no longer require restoration.
            }
        }
    }
}
