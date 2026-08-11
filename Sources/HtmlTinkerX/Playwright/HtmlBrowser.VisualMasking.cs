using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            const stateKey = 'htmltinkerxVisualMask' + token;
            let state = globalThis[stateKey];
            if (!state) {
                state = { masked: [], overlays: [], seen: new WeakSet() };
                Object.defineProperty(globalThis, stateKey, { value: state, configurable: true });
            }
            const roots = [document];
            for (let index = 0; index < roots.length; index++) {
                for (const element of roots[index].querySelectorAll('*')) {
                    if (element.shadowRoot && !roots.includes(element.shadowRoot)) roots.push(element.shadowRoot);
                }
            }
            const selections = [];
            for (const selector of selectors || []) {
                if (!selector || !selector.trim()) continue;
                const elements = roots
                    .flatMap(root => Array.from(root.querySelectorAll(selector)))
                    .filter(element => !element.hasAttribute(overlayMarker));
                selections.push(elements);
            }
            for (const elements of selections) {
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
        CancellationToken cancellationToken,
        bool freezePageScriptsDuringAction = false) {
        string[] selectors = CreateVisualMaskSelectors(maskSensitiveElements, maskSelectors);
        if (selectors.Length == 0) {
            return await action().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        TemporaryVisualMask mask = await ApplyTemporaryVisualMaskAsync(page, selectors, maskColor, cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (freezePageScriptsDuringAction) {
                await mask.SetPageScriptExecutionDisabledAsync(disabled: true).ConfigureAwait(false);
            }
            return await action().ConfigureAwait(false);
        } finally {
            if (freezePageScriptsDuringAction) {
                try {
                    await mask.SetPageScriptExecutionDisabledAsync(disabled: false).ConfigureAwait(false);
                } catch (PlaywrightException) when (cancellationToken.IsCancellationRequested || page.IsClosed) {
                    // Cancellation closes the page to interrupt printing; there is no live realm to restore.
                }
            }
            try {
                await mask.DisposeAsync().ConfigureAwait(false);
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

    private static async Task<TemporaryVisualMask> ApplyTemporaryVisualMaskAsync(IPage page, string[] selectors, string? maskColor, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string color = string.IsNullOrWhiteSpace(maskColor) ? "#000000" : maskColor!;
        string token = Guid.NewGuid().ToString("N");
        string arguments = JsonSerializer.Serialize(new {
            selectors,
            color,
            token
        });
        ICDPSession session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        List<VisualMaskExecutionContext> executionContexts = new();
        try {
            IReadOnlyList<string> frameIds = await GetFrameIdsAsync(session).ConfigureAwait(false);
            foreach (string frameId in frameIds) {
                cancellationToken.ThrowIfCancellationRequested();
                VisualMaskExecutionContext? executionContext = await ApplyVisualMaskToFrameAsync(
                    session,
                    frameId,
                    arguments).ConfigureAwait(false);
                if (executionContext.HasValue) executionContexts.Add(executionContext.Value);
            }
        } catch (Exception setupError) {
            Exception? cleanupError = null;
            try { await RemoveTemporaryVisualMaskAsync(session, executionContexts, token).ConfigureAwait(false); }
            catch (Exception error) { cleanupError = error; }
            try { await session.DetachAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanupError = cleanupError == null ? error : new AggregateException(cleanupError, error); }
            if (cleanupError != null) throw new AggregateException(setupError, cleanupError);
            throw;
        }
        return new TemporaryVisualMask(session, executionContexts, token);
    }

    private static async Task<VisualMaskExecutionContext?> ApplyVisualMaskToFrameAsync(
        ICDPSession session,
        string frameId,
        string arguments) {
        for (int attempt = 0; attempt < 2; attempt++) {
            try {
                JsonElement? worldResult = await session.SendAsync("Page.createIsolatedWorld", new Dictionary<string, object> {
                    ["frameId"] = frameId,
                    ["worldName"] = "HtmlTinkerX.VisualMask",
                    ["grantUniversalAccess"] = false
                }).ConfigureAwait(false);
                if (!worldResult.HasValue
                    || !worldResult.Value.TryGetProperty("executionContextId", out JsonElement contextIdElement)
                    || !contextIdElement.TryGetInt32(out int contextId)) {
                    throw new PlaywrightException("Chromium did not create an isolated execution world for visual masking.");
                }
                await EvaluateInIsolatedWorldAsync(session, contextId, $"({ApplyVisualMaskScript})({arguments})").ConfigureAwait(false);
                return new VisualMaskExecutionContext(frameId, contextId);
            } catch (PlaywrightException) {
                if (!await IsFramePresentAsync(session, frameId).ConfigureAwait(false)) return null;
                if (attempt != 0) throw;
            }
        }
        return null;
    }

    private static async Task<IReadOnlyList<string>> GetFrameIdsAsync(ICDPSession session) {
        JsonElement? treeResult = await session.SendAsync("Page.getFrameTree").ConfigureAwait(false);
        if (!treeResult.HasValue || !treeResult.Value.TryGetProperty("frameTree", out JsonElement frameTree)) {
            throw new PlaywrightException("Chromium did not return a frame tree for visual masking.");
        }
        List<string> frameIds = new();
        AddFrameIds(frameTree, frameIds);
        return frameIds;
    }

    private static async Task<bool> IsFramePresentAsync(ICDPSession session, string frameId) =>
        (await GetFrameIdsAsync(session).ConfigureAwait(false)).Contains(frameId, StringComparer.Ordinal);

    private static void AddFrameIds(JsonElement frameTree, List<string> frameIds) {
        if (frameTree.TryGetProperty("frame", out JsonElement frame)
            && frame.TryGetProperty("id", out JsonElement id)
            && !string.IsNullOrWhiteSpace(id.GetString())) {
            frameIds.Add(id.GetString()!);
        }
        if (!frameTree.TryGetProperty("childFrames", out JsonElement children)
            || children.ValueKind != JsonValueKind.Array) return;
        foreach (JsonElement child in children.EnumerateArray()) AddFrameIds(child, frameIds);
    }

    private static async Task EvaluateInIsolatedWorldAsync(ICDPSession session, int executionContextId, string expression) {
        JsonElement? result = await session.SendAsync("Runtime.evaluate", new Dictionary<string, object> {
            ["expression"] = expression,
            ["contextId"] = executionContextId,
            ["awaitPromise"] = true,
            ["returnByValue"] = true
        }).ConfigureAwait(false);
        if (result.HasValue && result.Value.TryGetProperty("exceptionDetails", out JsonElement exception)) {
            string message = exception.TryGetProperty("exception", out JsonElement thrown)
                && thrown.TryGetProperty("description", out JsonElement description)
                ? description.GetString() ?? "Visual masking failed in Chromium's isolated world."
                : "Visual masking failed in Chromium's isolated world.";
            throw new PlaywrightException(message);
        }
    }

    private static async Task RemoveTemporaryVisualMaskAsync(
        ICDPSession session,
        IReadOnlyList<VisualMaskExecutionContext> executionContexts,
        string token) {
        const string restoreScript =
            @"({ token }) => {
                const stateKey = 'htmltinkerxVisualMask' + token;
                const state = globalThis[stateKey];
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
                delete globalThis[stateKey];
            }";
        string arguments = JsonSerializer.Serialize(new { token });
        foreach (VisualMaskExecutionContext executionContext in executionContexts) {
            try {
                await EvaluateInIsolatedWorldAsync(
                    session,
                    executionContext.ExecutionContextId,
                    $"({restoreScript})({arguments})").ConfigureAwait(false);
            } catch (PlaywrightException error) {
                if (!await IsFramePresentAsync(session, executionContext.FrameId).ConfigureAwait(false)
                    || IsMissingExecutionContext(error)) continue;
                throw;
            }
        }
    }

    private static bool IsMissingExecutionContext(PlaywrightException error) =>
        error.Message.IndexOf("Cannot find context with specified id", StringComparison.OrdinalIgnoreCase) >= 0
        || error.Message.IndexOf("Execution context was destroyed", StringComparison.OrdinalIgnoreCase) >= 0
        || error.Message.IndexOf("Cannot find execution context", StringComparison.OrdinalIgnoreCase) >= 0;

    private readonly struct VisualMaskExecutionContext {
        internal VisualMaskExecutionContext(string frameId, int executionContextId) {
            FrameId = frameId;
            ExecutionContextId = executionContextId;
        }

        internal string FrameId { get; }
        internal int ExecutionContextId { get; }
    }

    private sealed class TemporaryVisualMask : IAsyncDisposable {
        private readonly ICDPSession _session;
        private readonly IReadOnlyList<VisualMaskExecutionContext> _executionContexts;
        private readonly string _token;
        private int _disposed;

        internal TemporaryVisualMask(
            ICDPSession session,
            IReadOnlyList<VisualMaskExecutionContext> executionContexts,
            string token) {
            _session = session;
            _executionContexts = executionContexts;
            _token = token;
        }

        internal Task SetPageScriptExecutionDisabledAsync(bool disabled) =>
            _session.SendAsync("Emulation.setScriptExecutionDisabled", new Dictionary<string, object> {
                ["value"] = disabled
            });

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Exception? cleanupError = null;
            try { await RemoveTemporaryVisualMaskAsync(_session, _executionContexts, _token).ConfigureAwait(false); }
            catch (Exception error) { cleanupError = error; }
            try { await _session.DetachAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanupError = cleanupError == null ? error : new AggregateException(cleanupError, error); }
            if (cleanupError != null) throw cleanupError;
        }
    }
}
