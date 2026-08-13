using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task RetainedPopupDomHandlesRouteToLiveStateAfterRelease() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            if (!(popup.document instanceof popup.Document) || Object.prototype.toString.call(popup.document) !== '[object HTMLDocument]') throw new Error('document facade identity lost');
            const image = popup.document.createElement('img');
            image.src = '{server.BlankPopupResourceUrl}?source=retained-attr-initial';
            popup.document.body.append(image);
            const attribute = image.getAttributeNode('src');
            const styled = popup.document.createElement('div');
            popup.document.body.append(styled);
            const map = styled.attributeStyleMap;
            const style = popup.document.createElement('style');
            popup.document.head.append(style);
            const sheet = style.sheet;
            popup.setTimeout(() => {{
                attribute.value = '{server.BlankPopupResourceUrl}?source=retained-attr-live';
                map.set('background-image', 'url({server.BlankPopupResourceUrl}?source=retained-typed-om)');
                if (sheet !== style.sheet) throw new Error('stylesheet facade identity lost');
                sheet.insertRule('@import url({server.BlankPopupResourceUrl}?source=retained-sheet-direct);');
            }}, 0);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 2000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("retained-attr-live"));
        Assert.Equal(1, server.BlankPopupSourceRequests("retained-typed-om"));
        Assert.Equal(1, server.BlankPopupSourceRequests("retained-sheet-direct"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupReplayPreservesLaterStyleTextAndRetainedNodesAcrossDocumentOpen() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const stylePopup = window.open('', '_blank');
            const style = stylePopup.document.createElement('style');
            stylePopup.document.head.append(style);
            const base = stylePopup.document.createElement('base');
            base.href = '{server.BlankPopupResourceUrl}';
            stylePopup.document.head.append(base);
            style.textContent = '@import url(?source=style-after-base);';
            const popup = window.open('', '_blank');
            const retained = popup.document.createElement('img');
            retained.src = '{server.BlankPopupResourceUrl}?source=retained-after-open';
            popup.document.open();
            popup.document.write('<html><head></head><body><span id=""retained-position""></span></body></html>');
            popup.document.close();
            popup.document.querySelector('#retained-position').replaceWith(retained);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("style-after-base"));
        Assert.Equal(1, server.BlankPopupSourceRequests("retained-after-open"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupSplitDocumentWritesCompleteParserOwnedStyleText() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            popup.document.write('<style>@imp');
            popup.document.write('ort url({server.BlankPopupResourceUrl}?source=split-write-style)</style>');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("split-write-style"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
