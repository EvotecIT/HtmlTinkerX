using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task BlankPopupWorkerValidatesAndTransfersMessagesSynchronously() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            const worker = new popup.Worker('/popup/worker.js');
            let cloneRejected = false;
            let originRejected = false;
            try { worker.postMessage(() => true); } catch (error) { cloneRejected = error.name === 'DataCloneError'; }
            try { new popup.Worker('https://example.invalid/worker.js'); } catch (error) { originRejected = error.name === 'SecurityError'; }
            const buffer = new ArrayBuffer(8);
            worker.postMessage({ buffer }, [buffer]);
            const transferDetached = buffer.byteLength === 0;
            document.querySelector('#result').textContent = cloneRejected && originRejected && transferDetached
                ? 'worker staging synchronous'
                : 'worker staging deferred';
            popup.close();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "worker staging synchronous");
    }

    [Fact]
    public async Task BlankPopupCloneTreesKeepResourceAttributesBehindInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const image = popup.document.createElement('img');
            const directClone = Node.prototype.cloneNode.call(image, false);
            directClone.src = '{server.BlankPopupResourceUrl}?source=direct-clone';
            const container = popup.document.createElement('div');
            container.innerHTML = '<span><img src=""{server.BlankPopupResourceUrl}?source=deep-clone""></span>';
            const deepClone = container.cloneNode(true);
            const deepImage = deepClone.querySelector('img');
            const attributesPreserved = directClone.getAttribute('src') === '{server.BlankPopupResourceUrl}?source=direct-clone'
                && deepImage.getAttribute('src') === '{server.BlankPopupResourceUrl}?source=deep-clone';
            document.querySelector('#result').textContent = attributesPreserved ? 'clone state preserved' : 'clone state lost';
            popup.document.body.append(directClone, deepClone);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        AssertPdfContains(result.PdfBytes, "clone state preserved");
        Assert.True(server.BlankPopupResourceRequests >= 2);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task OversizedStagedBeaconReturnsFalseWithoutQueuingARequest() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const accepted = popup.navigator.sendBeacon('{server.BlankPopupResourceUrl}', 'x'.repeat(64 * 1024 + 1));
            document.querySelector('#result').textContent = accepted ? 'oversized beacon accepted' : 'oversized beacon rejected';
            popup.close();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "oversized beacon rejected");
        Assert.Equal(0, server.BlankPopupResourceRequests);
    }
}
