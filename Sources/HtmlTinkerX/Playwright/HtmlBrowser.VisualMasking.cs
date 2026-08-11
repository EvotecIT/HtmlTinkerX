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
        @"({ selectors, color, token }) => {
            const overlayMarker = 'data-htmltinkerx-visual-mask-overlay';
            const stateKey = Symbol.for('htmltinkerx-visual-mask:' + token);
            let state = document[stateKey];
            if (!state) {
                state = { masked: [], overlays: [], seen: new WeakSet() };
                Object.defineProperty(document, stateKey, { value: state, configurable: true });
            }
            for (const selector of selectors || []) {
                if (!selector || !selector.trim()) continue;
                let elements = [];
                try {
                    elements = Array.from(document.querySelectorAll(selector))
                        .filter(element => !element.hasAttribute(overlayMarker));
                } catch { continue; }
                for (const element of elements) {
                    if (!(element instanceof Element) || !element.style) continue;
                    if (state.seen.has(element)) continue;
                    state.seen.add(element);
                    state.masked.push({ element, style: element.getAttribute('style') });
                    const rect = element.getBoundingClientRect();
                    const computed = getComputedStyle(element);
                    element.style.setProperty('visibility', 'hidden', 'important');

                    const overlay = document.createElement('div');
                    overlay.setAttribute(overlayMarker, token);
                    overlay.style.setProperty('position', computed.position === 'fixed' ? 'fixed' : 'absolute', 'important');
                    overlay.style.setProperty('left', `${rect.left + (computed.position === 'fixed' ? 0 : window.scrollX)}px`, 'important');
                    overlay.style.setProperty('top', `${rect.top + (computed.position === 'fixed' ? 0 : window.scrollY)}px`, 'important');
                    overlay.style.setProperty('width', `${rect.width}px`, 'important');
                    overlay.style.setProperty('height', `${rect.height}px`, 'important');
                    overlay.style.setProperty('margin', '0', 'important');
                    overlay.style.setProperty('padding', '0', 'important');
                    overlay.style.setProperty('border', '0', 'important');
                    overlay.style.setProperty('background-color', '#000000', 'important');
                    overlay.style.setProperty('background-image', `linear-gradient(${color}, ${color})`, 'important');
                    overlay.style.setProperty('box-shadow', 'none', 'important');
                    overlay.style.setProperty('filter', 'none', 'important');
                    overlay.style.setProperty('opacity', '1', 'important');
                    overlay.style.setProperty('visibility', 'visible', 'important');
                    overlay.style.setProperty('pointer-events', 'none', 'important');
                    overlay.style.setProperty('z-index', '2147483647', 'important');
                    overlay.style.setProperty('print-color-adjust', 'exact', 'important');
                    overlay.style.setProperty('-webkit-print-color-adjust', 'exact', 'important');
                    document.documentElement.appendChild(overlay);
                    state.overlays.push(overlay);
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
        (string token, IReadOnlyList<IFrame> maskedChildFrames) = await ApplyTemporaryVisualMaskAsync(page, selectors, maskColor, cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            return await action().ConfigureAwait(false);
        } finally {
            try {
                await RemoveTemporaryVisualMaskAsync(page, maskedChildFrames, token).ConfigureAwait(false);
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

    private static async Task<(string Token, IReadOnlyList<IFrame> MaskedChildFrames)> ApplyTemporaryVisualMaskAsync(IPage page, string[] selectors, string? maskColor, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string color = string.IsNullOrWhiteSpace(maskColor) ? "#000000" : maskColor!;
        string token = Guid.NewGuid().ToString("N");
        object arguments = new {
            selectors,
            color,
            token
        };
        await page.EvaluateAsync(ApplyVisualMaskScript, arguments).ConfigureAwait(false);

        List<IFrame> maskedChildFrames = new();
        IReadOnlyList<IFrame>? frames = page.Frames;
        if (frames == null) return (token, maskedChildFrames);
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
            try { await RemoveTemporaryVisualMaskAsync(page, maskedChildFrames, token).ConfigureAwait(false); } catch (PlaywrightException) { }
            throw;
        }
        return (token, maskedChildFrames);
    }

    private static async Task RemoveTemporaryVisualMaskAsync(IPage page, IReadOnlyList<IFrame> maskedChildFrames, string token) {
        const string restoreScript =
            @"({ token }) => {
                const stateKey = Symbol.for('htmltinkerx-visual-mask:' + token);
                const state = document[stateKey];
                if (!state) return;
                for (const overlay of state.overlays) overlay.remove();
                for (const item of state.masked) {
                    if (!(item.element instanceof Element) || !item.element.style) continue;
                    if (item.style === null) {
                        item.element.removeAttribute('style');
                    } else {
                        item.element.setAttribute('style', item.style);
                    }
                }
                delete document[stateKey];
            }";
        object arguments = new { token };
        await page.EvaluateAsync(restoreScript, arguments).ConfigureAwait(false);
        foreach (IFrame frame in maskedChildFrames) {
            try {
                await frame.EvaluateAsync(restoreScript, arguments).ConfigureAwait(false);
            } catch (PlaywrightException) when (frame.IsDetached) {
                // Detached frames no longer require restoration.
            }
        }
    }
}
