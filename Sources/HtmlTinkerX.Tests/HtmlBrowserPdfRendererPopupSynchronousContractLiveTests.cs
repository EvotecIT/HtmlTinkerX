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

    [Fact]
    public async Task OpenerRealmSendBeaconCannotBypassPopupStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const openerSendBeacon = Navigator.prototype.sendBeacon;
            const popup = window.open('', '_blank');
            const accepted = openerSendBeacon.call(popup.navigator, '{server.BlankPopupResourceUrl}', 'beacon');
            document.querySelector('#result').textContent = accepted ? 'opener beacon staged' : 'opener beacon rejected';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "opener beacon staged");
        Assert.True(server.BlankPopupResourceRequests >= 1);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task ExistingNodesReturnedByPopupQueriesRemainBehindStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const body = popup.document.querySelector('body');
            const bodyFromList = popup.document.querySelectorAll('body')[0];
            body.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=query-result"">';
            document.querySelector('#result').textContent = body === bodyFromList ? 'query nodes guarded' : 'query identity lost';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "query nodes guarded");
        Assert.True(server.BlankPopupResourceRequests >= 1);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task StagedXhrSnapshotsMutableBodiesAtSendTime() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const body = new popup.FormData();
            body.append('value', 'before');
            const request = new popup.XMLHttpRequest();
            request.open('POST', '{server.BlankPopupResourceUrl}?echo-body');
            request.onload = () => document.querySelector('#result').textContent = request.responseText;
            request.send(body);
            body.set('value', 'after');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "before");
        AssertPdfDoesNotContain(result.PdfBytes, "after");
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task StagedXhrRejectsConfigurationChangesAfterSend() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const request = new popup.XMLHttpRequest();
            request.open('POST', '{server.BlankPopupResourceUrl}?configuration-frozen');
            request.send('body');
            let headerRejected = false;
            let credentialsRejected = false;
            try {{ request.setRequestHeader('X-Late', 'changed'); }} catch (error) {{ headerRejected = error.name === 'InvalidStateError'; }}
            try {{ request.withCredentials = true; }} catch (error) {{ credentialsRejected = error.name === 'InvalidStateError'; }}
            request.abort();
            document.querySelector('#result').textContent = headerRejected && credentialsRejected
                ? 'xhr settings locked'
                : 'xhr settings changed';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "xhr settings locked");
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task BlankPopupCssomResourcesWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const body = popup.document.querySelector('body');
            body.style.backgroundImage = 'url({server.BlankPopupResourceUrl}?source=cssom)';
            body.style.color = 'red';
            document.querySelector('#result').textContent = body.getAttribute('style').includes('background-image')
                ? 'cssom state staged'
                : 'cssom state lost';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "cssom state staged");
        Assert.True(server.BlankPopupResourceRequests >= 1);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task BlankPopupLocationAndEventSourceExposeSynchronousState() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            let invalidLocationCaught = false;
            try { popup.location.assign('http://['); } catch { invalidLocationCaught = true; }
            const source = new popup.EventSource('/popup/events');
            source.close();
            globalThis.popupSynchronousState = invalidLocationCaught && source.readyState === popup.EventSource.CLOSED;
            popup.location.assign('/blank-popup-location');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => globalThis.popupSynchronousState === true && document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Null(server.LastPopupEventToken);
    }

    [Fact]
    public async Task StagedFetchSnapshotsUrlOptionsAndBodyAtCallTime() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const url = new popup.URL('{server.BlankPopupResourceUrl}?echo-body');
            const body = new popup.FormData();
            body.append('value', 'before');
            popup.fetch(url, {{ method: 'POST', body }}).then(response => response.text()).then(text => document.querySelector('#result').textContent = text).catch(error => document.querySelector('#result').textContent = error.name + ': ' + error.message);
            url.pathname = '/changed-after-fetch';
            body.set('value', 'after');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "before");
        AssertPdfDoesNotContain(result.PdfBytes, "after");
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task StagedDocumentWriteRunsInlineScriptsAtParserPosition() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.document.write('<script>const laterAbsent=!document.querySelector(""#later"");document.write(""<span id=written>nested write</span>"");opener.document.querySelector(""#result"").textContent=laterAbsent&&document.querySelector(""#written"")?""parser order preserved"":""parser order broken"";</script><p id=later>later</p>');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "parser order preserved");
    }
}
