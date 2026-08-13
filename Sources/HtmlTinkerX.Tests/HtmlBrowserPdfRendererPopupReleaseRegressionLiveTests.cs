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
        string script = $@"window.__htmlTinkerXSyntheticClickCount = 0;
            const createSubmitter = (popup, source) => {{
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
            direct.addEventListener('click', () => window.__htmlTinkerXSyntheticClickCount++);
            if (!direct.dispatchEvent(new directPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('synthetic click was unexpectedly cancelled');
            const borrowedPopup = window.open('', '_blank');
            const borrowed = createSubmitter(borrowedPopup, 'synthetic-click-submit-borrowed');
            borrowed.addEventListener('click', () => window.__htmlTinkerXSyntheticClickCount++);
            if (!EventTarget.prototype.dispatchEvent.call(borrowed, new borrowedPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('borrowed synthetic click was unexpectedly cancelled');
            const anchorPopup = window.open('', '_blank');
            const anchor = anchorPopup.document.createElement('a');
            anchor.href = '{server.BlankPopupResourceUrl}?source=synthetic-click-anchor';
            anchor.target = '_self';
            anchorPopup.document.body.append(anchor);
            anchor.addEventListener('click', () => window.__htmlTinkerXSyntheticClickCount++);
            if (!anchor.dispatchEvent(new anchorPopup.MouseEvent('click', {{ bubbles: true, cancelable: true }}))) throw new Error('synthetic anchor click was unexpectedly cancelled');
            if (window.__htmlTinkerXSyntheticClickCount !== 3) throw new Error('synthetic listeners were not dispatched synchronously');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => window.__htmlTinkerXSyntheticClickCount === 3",
                timeout: 10000),
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
            const legacyStyle = popup.document.createElement('style');
            popup.document.head.append(legacyStyle);
            legacyStyle.sheet.addRule('#legacy-css-target', 'background-image: url({server.BlankPopupResourceUrl}?source=stylesheet-legacy)');
            legacyStyle.sheet.addRule('#removed-css-target', 'background-image: url({server.BlankPopupResourceUrl}?source=stylesheet-legacy-removed)');
            legacyStyle.sheet.removeRule(1);
            const legacyTarget = popup.document.createElement('div');
            legacyTarget.id = 'legacy-css-target';
            popup.document.body.append(legacyTarget);
            const disabledStyle = popup.document.createElement('style');
            disabledStyle.textContent = '#disabled-css-target {{ background-image: url({server.BlankPopupResourceUrl}?source=stylesheet-disabled); }}';
            popup.document.head.append(disabledStyle);
            disabledStyle.sheet.disabled = true;
            const disabledTarget = popup.document.createElement('div');
            disabledTarget.id = 'disabled-css-target';
            popup.document.body.append(disabledTarget);
            window.__htmlTinkerXStyleElement = style;
            window.__htmlTinkerXLegacyStyleElement = legacyStyle;
            window.__htmlTinkerXDisabledStyleElement = disabledStyle;
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: @"() => {
                    const rules = Array.from(window.__htmlTinkerXStyleElement?.sheet?.cssRules || [], rule => rule.cssText);
                    if (rules.length !== 2 || rules.filter(rule => rule.includes('color: red')).length !== 1) throw new Error('base:' + rules.length);
                    if (window.__htmlTinkerXLegacyStyleElement?.sheet?.cssRules?.length !== 1) throw new Error('legacy:' + window.__htmlTinkerXLegacyStyleElement?.sheet?.cssRules?.length);
                    if (window.__htmlTinkerXDisabledStyleElement?.sheet?.disabled !== true) throw new Error('disabled:' + window.__htmlTinkerXDisabledStyleElement?.sheet?.disabled);
                    document.querySelector('#result').textContent = 'stylesheet restored once';
                    return true;
                }",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "stylesheet restored once");
        Assert.Equal(1, server.BlankPopupSourceRequests("stylesheet-initial"));
        Assert.Equal(1, server.BlankPopupSourceRequests("stylesheet-inserted"));
        Assert.Equal(1, server.BlankPopupSourceRequests("stylesheet-legacy"));
        Assert.Equal(0, server.BlankPopupSourceRequests("stylesheet-legacy-removed"));
        Assert.Equal(0, server.BlankPopupSourceRequests("stylesheet-disabled"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task DirectFormSubmissionSnapshotsInvocationState() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const directPopup = window.open('', '_blank');
            const direct = directPopup.document.createElement('form');
            direct.method = 'post';
            direct.action = '{server.BlankPopupResourceUrl}?source=direct-submit-snapshot';
            directPopup.document.body.append(direct);
            direct.submit();
            direct.action = '{server.BlankPopupResourceUrl}?source=direct-submit-mutated';
            direct.remove();
            const requestPopup = window.open('', '_blank');
            const request = requestPopup.document.createElement('form');
            request.method = 'post';
            request.action = '{server.BlankPopupResourceUrl}?source=request-submit-snapshot';
            const button = requestPopup.document.createElement('button');
            button.type = 'submit';
            button.name = 'chosen';
            button.value = 'original';
            request.append(button);
            requestPopup.document.body.append(request);
            request.requestSubmit(button);
            request.action = '{server.BlankPopupResourceUrl}?source=request-submit-mutated';
            button.remove();
            request.remove();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("direct-submit-snapshot"));
        Assert.Equal(1, server.BlankPopupSourceRequests("request-submit-snapshot"));
        Assert.Equal(0, server.BlankPopupSourceRequests("direct-submit-mutated"));
        Assert.Equal(0, server.BlankPopupSourceRequests("request-submit-mutated"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupFactoriesAndParentNodeMutationsRemainStaged() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const directStyle = popup.document.createElement('style');
            popup.document.head.append(directStyle);
            directStyle.insertAdjacentText('beforeend', '@import url({server.BlankPopupResourceUrl}?source=adjacent-style);');
            const borrowedStyle = popup.document.createElement('style');
            popup.document.head.append(borrowedStyle);
            Element.prototype.insertAdjacentText.call(borrowedStyle, 'afterbegin', '@import url({server.BlankPopupResourceUrl}?source=adjacent-style-borrowed);');
            const host = popup.document.createElement('div');
            popup.document.body.append(host);
            const shadow = host.attachShadow({{ mode: 'open' }});
            const foreignStyle = document.createElement('style');
            foreignStyle.textContent = '@import url({server.BlankPopupResourceUrl}?source=shadow-parent-node);';
            shadow.append(foreignStyle);
            const table = popup.document.createElement('table');
            popup.document.body.append(table);
            table.insertRow().innerHTML = `<td><img src='{server.BlankPopupResourceUrl}?source=table-row'></td>`;
            const body = table.createTBody();
            body.insertRow().insertCell().innerHTML = `<img src='{server.BlankPopupResourceUrl}?source=table-cell'>`;
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        foreach (string source in new[] { "adjacent-style", "adjacent-style-borrowed", "shadow-parent-node", "table-row", "table-cell" }) {
            Assert.Equal(1, server.BlankPopupSourceRequests(source));
        }
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task SyntheticDispatchReturnsPageCancellationBeforeRelease() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const form = popup.document.createElement('form');
            form.method = 'post';
            form.action = '{server.BlankPopupResourceUrl}?source=cancelled-dispatch-fallback';
            const button = popup.document.createElement('button');
            button.type = 'submit';
            form.append(button);
            popup.document.body.append(form);
            button.addEventListener('click', event => event.preventDefault());
            const directEvent = new popup.MouseEvent('click', {{ bubbles: true, cancelable: true }});
            if (button.dispatchEvent(directEvent)) form.submit();
            if (!directEvent.defaultPrevented) throw new Error('direct cancellation was not observable');
            const borrowed = popup.document.createElement('a');
            borrowed.href = '{server.BlankPopupResourceUrl}?source=cancelled-dispatch-anchor';
            borrowed.target = '_self';
            popup.document.body.append(borrowed);
            borrowed.addEventListener('click', event => Event.prototype.preventDefault.call(event));
            const borrowedEvent = new popup.MouseEvent('click', {{ bubbles: true, cancelable: true }});
            const borrowedResult = EventTarget.prototype.dispatchEvent.call(borrowed, borrowedEvent);
            if (borrowedResult) borrowed.click();
            if (borrowedResult) throw new Error('borrowed cancellation did not affect dispatch result');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 750),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(0, server.BlankPopupSourceRequests("cancelled-dispatch-fallback"));
        Assert.Equal(0, server.BlankPopupSourceRequests("cancelled-dispatch-anchor"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
