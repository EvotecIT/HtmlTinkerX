using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task PopupStagingCoversAnimationsClosedShadowMarkupCssomCustomElementsChildWindowsAndAreas() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            popup.document.write(`<div><template shadowrootmode='open'><img src='{server.BlankPopupResourceUrl}?source=written-shadow'></template></div>`);
            const animated = popup.document.createElement('div');
            popup.document.body.append(animated);
            animated.animate({{ backgroundImage: 'url({server.BlankPopupResourceUrl}?source=web-animation)' }}, 1000);

            const closedHost = document.createElement('div');
            const closedRoot = closedHost.attachShadow({{ mode: 'closed' }});
            const closedStyle = document.createElement('style');
            closedStyle.textContent = '@import url({server.BlankPopupResourceUrl}?source=closed-shadow);';
            closedRoot.append(closedStyle);
            popup.document.body.append(closedHost);

            const style = document.createElement('style');
            popup.document.head.append(style);
            const cssTarget = popup.document.createElement('div');
            cssTarget.id = 'css-target';
            popup.document.body.append(cssTarget);
            style.sheet.insertRule('#css-target {{ color: black; }}');
            style.sheet.cssRules[0].style.setProperty('background-image', 'url({server.BlankPopupResourceUrl}?source=nested-cssom)');

            popup.customElements.define('x-htmltinkerx-resource', class extends popup.HTMLElement {{
                constructor() {{
                    super();
                    const image = popup.document.createElement('img');
                    image.src = '{server.BlankPopupResourceUrl}?source=custom-element';
                    this.append(image);
                }}
            }});
            popup.document.body.append(popup.document.createElement('x-htmltinkerx-resource'));

            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            frame.contentDocument.defaultView.fetch('{server.BlankPopupResourceUrl}?source=child-default-view');

            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 2000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        foreach (string source in new[] { "web-animation", "closed-shadow", "written-shadow", "nested-cssom", "custom-element", "child-default-view" }) {
            int requests = server.BlankPopupSourceRequests(source);
            Assert.True(requests is >= 1 and <= 2, $"Unexpected request count for {source}: {requests}.");
        }
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task HtmlAreaPopupWaitsForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const map = document.createElement('map');
            map.name = 'htmltinkerx-map';
            const area = document.createElement('area');
            area.href = new URL('/blank-popup-location', location.href).href;
            area.target = '_blank';
            map.append(area);
            document.body.append(map);
            area.click();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal("popup-token", server.LastPopupToken);
    }

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
