using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task IndexedAndNamedChildWindowsRemainBehindStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const frame = popup.document.createElement('iframe');
            frame.name = 'reportFrame';
            popup.document.body.append(frame);
            popup[0].fetch('{server.BlankPopupResourceUrl}?source=indexed-child-window');
            popup.frames.reportFrame.fetch('{server.BlankPopupResourceUrl}?source=named-child-window');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("indexed-child-window"));
        Assert.Equal(1, server.BlankPopupSourceRequests("named-child-window"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task RangeCreatedResourcesRemainBehindStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const range = popup.document.createRange();
            range.selectNodeContents(popup.document.body);
            const fragment = range.createContextualFragment('<img src=""{server.BlankPopupResourceUrl}?source=range-fragment"">');
            range.insertNode(fragment);
            const borrowedFragment = Range.prototype.createContextualFragment.call(range, '<img src=""{server.BlankPopupResourceUrl}?source=range-borrowed"">');
            Range.prototype.insertNode.call(range, borrowedFragment);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("range-fragment"));
        Assert.Equal(1, server.BlankPopupSourceRequests("range-borrowed"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task GuardedAnchorActivationRunsAfterHrefRestoration() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = @"const popup = window.open('', '_blank');
            const anchor = popup.document.createElement('a');
            anchor.href = '/blank-popup-location';
            anchor.target = '_self';
            popup.document.body.append(anchor);
            anchor.click();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task PopupPaintWorkletLoadsOnlyAfterHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            if (!popup.CSS?.paintWorklet) throw new Error('paint worklet unavailable');
            const targetBase = popup.document.createElement('base');
            targetBase.target = '_blank';
            popup.document.head.append(targetBase);
            const base = popup.document.createElement('base');
            base.href = '{server.BlankPopupResourceUrl}';
            popup.document.head.append(base);
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            const childBase = frame.contentDocument.createElement('base');
            childBase.href = '{server.BlankPopupResourceUrl}';
            frame.contentDocument.head.append(childBase);
            const openerAddModule = Object.getPrototypeOf(CSS.paintWorklet).addModule;
            Promise.all([
                popup.CSS.paintWorklet.addModule('?source=paint-worklet'),
                openerAddModule.call(popup.CSS.paintWorklet, '?source=paint-worklet-borrowed'),
                frame.contentWindow.CSS.paintWorklet.addModule('?source=paint-worklet-child')
            ]).then(() => document.querySelector('#result').textContent = 'paint worklet loaded');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "paint worklet loaded");
        Assert.Equal(1, server.BlankPopupSourceRequests("paint-worklet"));
        Assert.Equal(1, server.BlankPopupSourceRequests("paint-worklet-borrowed"));
        Assert.Equal(1, server.BlankPopupSourceRequests("paint-worklet-child"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupDocumentMutationSurfacesRemainBehindStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const baseTarget = popup.document.createElement('base');
            baseTarget.target = '_blank';
            popup.document.head.append(baseTarget);
            const base = popup.document.createElement('base');
            base.href = '{server.BlankPopupResourceUrl}';
            popup.document.head.append(base);
            const style = popup.document.createElement('style');
            popup.document.head.append(style);
            popup.document.styleSheets[0].insertRule('@import url({server.BlankPopupResourceUrl}?source=document-stylesheet);', 0);
            for (const sheet of popup.document.styleSheets) sheet.insertRule('@import url({server.BlankPopupResourceUrl}?source=document-stylesheet-iterator);', 0);
            const selected = popup.document.createElement('span');
            selected.textContent = 'selection';
            popup.document.body.append(selected);
            const initial = popup.document.createRange();
            initial.selectNode(selected);
            const selection = popup.getSelection();
            selection.removeAllRanges();
            selection.addRange(initial);
            const selectedImage = popup.document.createElement('img');
            selectedImage.src = '{server.BlankPopupResourceUrl}?source=selection-range';
            selection.getRangeAt(0).insertNode(selectedImage);
            const clonedImage = popup.document.createElement('img');
            clonedImage.src = '{server.BlankPopupResourceUrl}?source=cloned-selection-range';
            selection.getRangeAt(0).cloneRange().insertNode(clonedImage);
            const host = popup.document.createElement('div');
            popup.document.body.append(host);
            const root = host.attachShadow({{ mode: 'open' }});
            const shadowStyle = document.createElement('style');
            shadowStyle.textContent = '@import url({server.BlankPopupResourceUrl}?source=shadow-insertion);';
            root.appendChild(shadowStyle);
            const adopted = document.createElement('img');
            popup.document.body.appendChild(adopted);
            adopted.src = '?source=adopted-node';
            popup.fetch(adopted.src);
            const editable = popup.document.createElement('div');
            editable.contentEditable = 'true';
            editable.textContent = 'edit';
            popup.document.body.append(editable);
            const editRange = popup.document.createRange();
            editRange.selectNodeContents(editable);
            selection.removeAllRanges();
            selection.addRange(editRange);
            editable.focus();
            let invalidExecCommandRejected = false;
            try {{ popup.document.execCommand(); }} catch (error) {{ invalidExecCommandRejected = error.name === 'TypeError'; }}
            if (!invalidExecCommandRejected) throw new Error('invalid execCommand did not fail synchronously');
            popup.document.execCommand('insertHTML', false, '<img src=""{server.BlankPopupResourceUrl}?source=exec-command"">');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("document-stylesheet"));
        Assert.Equal(1, server.BlankPopupSourceRequests("document-stylesheet-iterator"));
        Assert.Equal(1, server.BlankPopupSourceRequests("selection-range"));
        Assert.Equal(1, server.BlankPopupSourceRequests("cloned-selection-range"));
        Assert.Equal(1, server.BlankPopupSourceRequests("shadow-insertion"));
        Assert.Equal(2, server.BlankPopupSourceRequests("adopted-node"));
        Assert.Equal(1, server.BlankPopupSourceRequests("exec-command"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupFormMethodsWaitForStagedActions() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const directPopup = window.open('', '_blank');
            const directForm = directPopup.document.createElement('form');
            directForm.method = 'post';
            directForm.action = '{server.BlankPopupResourceUrl}?source=form-submit';
            directPopup.document.body.append(directForm);
            directForm.submit();
            const requestPopup = window.open('', '_blank');
            const requestForm = requestPopup.document.createElement('form');
            requestForm.method = 'post';
            requestForm.action = '{server.BlankPopupResourceUrl}?source=form-request-submit';
            const button = requestPopup.document.createElement('button');
            button.type = 'submit';
            requestForm.append(button);
            requestPopup.document.body.append(requestForm);
            let invalidSubmitterRejected = false;
            try {{ requestForm.requestSubmit({{ localName: 'button', type: 'submit', form: requestForm }}); }} catch (error) {{ invalidSubmitterRejected = error.name === 'TypeError'; }}
            if (!invalidSubmitterRejected) throw new Error('forged submitter accepted');
            requestForm.requestSubmit(button);
            const clickPopup = window.open('', '_blank');
            const clickForm = clickPopup.document.createElement('form');
            clickForm.method = 'post';
            clickForm.action = '{server.BlankPopupResourceUrl}?source=form-button-click';
            const clickButton = clickPopup.document.createElement('button');
            clickButton.type = 'submit';
            clickForm.append(clickButton);
            clickPopup.document.body.append(clickForm);
            clickButton.click();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("form-submit"));
        Assert.Equal(1, server.BlankPopupSourceRequests("form-request-submit"));
        Assert.Equal(1, server.BlankPopupSourceRequests("form-button-click"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
