namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Installs renderer-owned capture styles without trusting page-modified DOM methods.</summary>
public static partial class HtmlBrowser {
    private const string ApplyCaptureStyleSheetScript =
        @"css => {
            const attribute = 'data-htmltinkerx-pdf-capture-style';
            const querySelector = Document.prototype.querySelector;
            const createElement = Document.prototype.createElement;
            const setAttribute = Element.prototype.setAttribute;
            const prepend = Element.prototype.prepend;
            const textContent = Object.getOwnPropertyDescriptor(Node.prototype, 'textContent');
            let style = querySelector.call(document, `style[${attribute}]`);
            if (!style) {
                style = createElement.call(document, 'style');
                setAttribute.call(style, attribute, '');
                prepend.call(document.head || document.documentElement, style);
            }
            if (textContent.get.call(style) !== css) textContent.set.call(style, css);
        }";

    internal static async Task ApplyCaptureStyleSheetAsync(
        IPage page,
        string styleSheetContent,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ICDPSession session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        try {
            IReadOnlyList<string> frameIds = await GetFrameIdsAsync(session).ConfigureAwait(false);
            string css = JsonSerializer.Serialize(styleSheetContent);
            foreach (string frameId in frameIds) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    JsonElement? world = await session.SendAsync("Page.createIsolatedWorld", new Dictionary<string, object> {
                        ["frameId"] = frameId,
                        ["worldName"] = "HtmlTinkerX.CaptureStyle",
                        ["grantUniversalAccess"] = false
                    }).ConfigureAwait(false);
                    if (!world.HasValue
                        || !world.Value.TryGetProperty("executionContextId", out JsonElement contextIdElement)
                        || !contextIdElement.TryGetInt32(out int contextId)) {
                        throw new PlaywrightException("Chromium did not create an isolated capture-style world.");
                    }
                    await EvaluateInIsolatedWorldAsync(
                        session,
                        contextId,
                        $"({ApplyCaptureStyleSheetScript})({css})").ConfigureAwait(false);
                } catch (PlaywrightException error) {
                    if (!await IsFramePresentAsync(session, frameId).ConfigureAwait(false)
                        || IsMissingExecutionContext(error)) continue;
                    throw;
                }
            }
        } finally {
            try { await session.DetachAsync().ConfigureAwait(false); } catch (PlaywrightException) when (page.IsClosed) { }
        }
    }
}
