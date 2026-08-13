using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task ConnectedStyleAndScriptTextPropertiesWaitForInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const style = popup.document.createElement('style');
            popup.document.head.append(style);
            style.innerText = '@import url({server.BlankPopupResourceUrl}?source=connected-style-inner-text);';
            const dynamic = popup.document.createElement('script');
            popup.document.head.append(dynamic);
            dynamic.text = `fetch('{server.BlankPopupResourceUrl}?source=connected-script-text')`;
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("connected-style-inner-text"));
        Assert.Equal(1, server.BlankPopupSourceRequests("connected-script-text"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task SyntheticClickDispatchWaitsForFormAttributeRestoration() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const createSubmitter = (popup, source) => {{
            const form = popup.document.createElement('form');
            form.method = 'post';
            form.action = `{server.BlankPopupResourceUrl}?source=${{source}}`;
            const button = popup.document.createElement('button');
            button.type = 'submit';
            form.append(button);
            popup.document.body.append(form);
            return button;
            }};
            const directPopup = window.open('', '_blank');
            const direct = createSubmitter(directPopup, 'synthetic-click-submit');
            if (!direct.dispatchEvent(new directPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('synthetic click was unexpectedly cancelled');
            const borrowedPopup = window.open('', '_blank');
            const borrowed = createSubmitter(borrowedPopup, 'synthetic-click-submit-borrowed');
            if (!EventTarget.prototype.dispatchEvent.call(borrowed, new borrowedPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('borrowed synthetic click was unexpectedly cancelled');
            const anchorPopup = window.open('', '_blank');
            const anchor = anchorPopup.document.createElement('a');
            anchor.href = '{server.BlankPopupResourceUrl}?source=synthetic-click-anchor';
            anchor.target = '_self';
            anchorPopup.document.body.append(anchor);
            if (!anchor.dispatchEvent(new anchorPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('synthetic anchor click was unexpectedly cancelled');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("synthetic-click-submit"));
        Assert.Equal(1, server.BlankPopupSourceRequests("synthetic-click-submit-borrowed"));
        Assert.Equal(1, server.BlankPopupSourceRequests("synthetic-click-anchor"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task StagedStyleSheetMutationsRestoreEachRuleOnce() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const style = popup.document.createElement('style');
            style.textContent = 'body {{ color: red; background-image: url({server.BlankPopupResourceUrl}?source=stylesheet-initial); }}';
            popup.document.head.append(style);
            style.sheet.insertRule('html {{ background-color: white; background-image: url({server.BlankPopupResourceUrl}?source=stylesheet-inserted); }}', 0);
            window.__htmlTinkerXStyleElement = style;
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: @"() => {
                    const rules = Array.from(window.__htmlTinkerXStyleElement?.sheet?.cssRules || [], rule => rule.cssText);
                    if (rules.length !== 2 || rules.filter(rule => rule.includes('color: red')).length !== 1) return false;
                    document.querySelector('#result').textContent = 'stylesheet restored once';
                    return true;
                }",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "stylesheet restored once");
        Assert.Equal(1, server.BlankPopupSourceRequests("stylesheet-initial"));
        Assert.Equal(1, server.BlankPopupSourceRequests("stylesheet-inserted"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
