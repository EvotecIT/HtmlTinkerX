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
            const style = popup.document.createElement('style');
            popup.document.head.append(style);
            popup.document.styleSheets[0].insertRule('@import url({server.BlankPopupResourceUrl}?source=document-stylesheet);', 0);
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
            const host = popup.document.createElement('div');
            popup.document.body.append(host);
            const root = host.attachShadow({{ mode: 'open' }});
            const shadowStyle = document.createElement('style');
            shadowStyle.textContent = '@import url({server.BlankPopupResourceUrl}?source=shadow-insertion);';
            root.appendChild(shadowStyle);
            const editable = popup.document.createElement('div');
            editable.contentEditable = 'true';
            editable.textContent = 'edit';
            popup.document.body.append(editable);
            const editRange = popup.document.createRange();
            editRange.selectNodeContents(editable);
            selection.removeAllRanges();
            selection.addRange(editRange);
            editable.focus();
            popup.document.execCommand('insertHTML', false, '<img src=""{server.BlankPopupResourceUrl}?source=exec-command"">');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("document-stylesheet"));
        Assert.Equal(1, server.BlankPopupSourceRequests("selection-range"));
        Assert.Equal(1, server.BlankPopupSourceRequests("shadow-insertion"));
        Assert.Equal(1, server.BlankPopupSourceRequests("exec-command"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
