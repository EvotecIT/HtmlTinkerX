using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task BlankPopupDoesNotExposeAnInvokablePrivateReleaseHandshake() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = @"const popup = window.open('', '_blank');
            for (const name of [...Object.getOwnPropertyNames(popup), ...Object.getOwnPropertySymbols(popup)]) {
                if ((String(name).toLowerCase().includes('htmltinkerx') || /^[0-9a-f]{32}$/.test(String(name))) && typeof popup[name] === 'function') popup[name]();
            }
            popup.window.location.href = '/header-popup';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task SynchronousPopupWritePreservesBlockingExternalScriptOrder() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.document.write('<script src=""/popup/blocking.js""></script><script>opener.document.querySelector(""#result"").textContent=globalThis.externalReady?""script order preserved"":""script order broken"";</script>');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'script order preserved' || document.querySelector('#result').textContent === 'script order broken'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "script order preserved");
    }

    [Fact]
    public async Task ExplicitBlankPopupPreservesCreatedNodeIdentityAndBindsWindowMethods() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            popup.focus();
            popup.postMessage('htmltinkerx-probe', '*');
            popup.document.write('<iframe id=""staged-frame""></iframe>');
            const frame = popup.document.getElementById('staged-frame');
            const nativeIdentity = frame instanceof popup.Node && popup.getComputedStyle(frame) !== null;
            frame.dataset.identity = 'retained';
            frame.addEventListener('load', event => {{
                document.querySelector('#result').textContent = nativeIdentity && event.currentTarget === frame
                    ? frame.dataset.identity
                    : 'identity lost';
                popup.close();
            }});
            frame.src = '{server.BlankPopupResourceUrl}';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'retained'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "retained");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task DetachedPopupResourceCreatedThroughTheFacadeWaitsForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const popupPrototypeImage = popup.document.createElement('img');
            Object.getPrototypeOf(popupPrototypeImage).setAttribute.call(popupPrototypeImage, 'src', '{server.BlankPopupResourceUrl}?source=popup-prototype');
            const idlImage = popup.document.createElement('img');
            Object.getOwnPropertyDescriptor(Object.getPrototypeOf(idlImage), 'src').set.call(idlImage, '{server.BlankPopupResourceUrl}?source=idl-setter');
            const openerPrototypeImage = popup.document.createElement('img');
            Element.prototype.setAttribute.call(openerPrototypeImage, 'src', '{server.BlankPopupResourceUrl}?source=opener-prototype');
            const namespaceImage = popup.document.createElement('img');
            Object.getPrototypeOf(namespaceImage).setAttributeNS.call(namespaceImage, null, 'src', '{server.BlankPopupResourceUrl}?source=namespace');
            const attributeImage = popup.document.createElement('img');
            const sourceAttribute = popup.document.createAttribute('src');
            sourceAttribute.value = '{server.BlankPopupResourceUrl}?source=attribute-node';
            attributeImage.setAttributeNode(sourceAttribute);
            const namedItemImage = popup.document.createElement('img');
            const namedItemAttribute = popup.document.createAttribute('src');
            namedItemAttribute.value = '{server.BlankPopupResourceUrl}?source=named-item';
            namedItemImage.attributes.setNamedItem(namedItemAttribute);
            const svgImage = popup.document.createElementNS('http://www.w3.org/2000/svg', 'image');
            const namespacedHref = popup.document.createAttributeNS('http://www.w3.org/1999/xlink', 'xlink:href');
            namespacedHref.value = '{server.BlankPopupResourceUrl}?source=removed-namespace';
            svgImage.attributes.setNamedItemNS(namespacedHref);
            svgImage.attributes.removeNamedItemNS('http://www.w3.org/1999/xlink', 'href');
            const removedImage = popup.document.createElement('img');
            Object.getPrototypeOf(removedImage).setAttributeNS.call(removedImage, null, 'src', '{server.BlankPopupResourceUrl}?source=removed');
            Object.getPrototypeOf(removedImage).removeAttributeNS.call(removedImage, null, 'src');
            true";

        await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 750),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.True(server.BlankPopupResourceRequests >= 6);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
        Assert.Equal(0, server.RemovedNamespacedResourceRequests);
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task NonblankPopupReferenceStagesRequestsUntilHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('/header-popup', '_blank');
            popup.fetch('{server.BlankPopupResourceUrl}');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, function: "() => document.querySelector('#result').textContent === 'popup authorized'", timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.True(server.BlankPopupResourceRequests > 0);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupClosedBeforeInterceptorAttachmentDoesNotFailCapture() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 250),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "const popup = window.open('', '_blank'); popup.close(); document.querySelector('#result').textContent = 'closed intentionally'; true"));

        AssertPdfContains(result.PdfBytes, "closed intentionally");
    }

    [Fact]
    public async Task CreatedPopupNodeDocumentRelationsRemainBehindHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            const node = popup.document.createElement('div');
            node.ownerDocument.defaultView.location.href = '/header-popup';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task BlankPopupRequestApisWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            popup.navigator.sendBeacon('{server.BlankPopupResourceUrl}', 'beacon');
            const request = new popup.XMLHttpRequest();
            request.open('POST', '{server.BlankPopupResourceUrl}');
            request.send('xhr');
            const image = new popup.Image();
            image.src = '{server.BlankPopupResourceUrl}';
            true";

        await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.True(server.BlankPopupResourceRequests >= 3);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task InvalidStagedBeaconFailsSynchronouslyWithoutDiscardingLaterWork() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            let rejected = false;
            try { popup.navigator.sendBeacon('http://[invalid'); } catch { rejected = true; }
            if (rejected) popup.location.href = '/header-popup';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task BlankPopupXhrAbortBeforeReleaseCancelsTheQueuedSend() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const request = new popup.XMLHttpRequest();
            request.open('POST', '{server.BlankPopupResourceUrl}');
            request.send('xhr');
            request.abort();
            popup.location.href = '/blank-popup-location';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal(0, server.BlankPopupResourceRequests);
    }

    [Fact]
    public async Task BlankPopupXhrOpenReplacesAnEarlierQueuedSend() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const request = new popup.XMLHttpRequest();
            request.open('POST', '{server.BlankPopupResourceUrl}?superseded');
            request.send('old');
            let duplicateRejected = false;
            try {{ request.send('duplicate'); }} catch {{ duplicateRejected = true; }}
            document.querySelector('#result').textContent = duplicateRejected ? 'xhr lifecycle preserved' : 'duplicate send accepted';
            request.open('POST', '{server.BlankPopupResourceUrl}?current');
            request.send('new');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "xhr lifecycle preserved");
        Assert.Equal(1, server.BlankPopupResourceRequests);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task BlankPopupLocationValueOfRemainsBehindHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.location.valueOf().href = '/blank-popup-location';
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
    public async Task BlankPopupWorkerWaitsForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            const worker = new popup.Worker('/popup/worker.js');
            const nativeIdentity = worker instanceof popup.Worker;
            worker.onmessage = event => document.querySelector('#result').textContent = nativeIdentity ? event.data : 'worker identity lost';
            worker.postMessage('start');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup worker authorized");
        Assert.Equal("popup-token", server.LastPopupWorkerToken);
    }

    [Fact]
    public async Task BlankPopupEventSourceWaitsForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            const source = new popup.EventSource('/popup/events');
            const nativeIdentity = source instanceof popup.EventSource;
            source.onmessage = event => {
                document.querySelector('#result').textContent = nativeIdentity ? event.data : 'event source identity lost';
                source.close();
            };
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup event authorized");
        Assert.Equal("popup-token", server.LastPopupEventToken);
    }

    [Theory]
    [InlineData("Worker")]
    [InlineData("EventSource")]
    public async Task InvalidStagedConstructorThrowsSynchronouslyWithoutBlockingLaterNavigation(string constructorName) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            let caught = false;
            try {{ new popup.{constructorName}('http://['); }} catch {{ caught = true; }}
            globalThis.constructorFailureCaught = caught;
            popup.location.href = '/blank-popup-location';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => globalThis.constructorFailureCaught === true && document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWindowPrototypeOpenCannotBypassPopupHeaderInterception(bool deleteOwnOverride) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = deleteOwnOverride
            ? "delete window.open; Window.prototype.open.call(window, '/header-popup', '_blank'); true"
            : "Window.prototype.open.call(window, '/header-popup', '_blank'); true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task BlankPopupFetchUsesThePopupRealmBaseUrl() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.document.write('<base href=""/popup/""><p id=""popup-content"">ready</p>');
            const visibleSynchronously = popup.document.querySelector('#popup-content')?.textContent === 'ready';
            popup.fetch('fetch-result')
                .then(response => response.text())
                .then(text => document.querySelector('#result').textContent = visibleSynchronously ? text : 'popup DOM missing')
                .catch(error => document.querySelector('#result').textContent = 'popup fetch error: ' + error.message);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup fetch completed' || document.querySelector('#result').textContent === 'popup DOM missing' || document.querySelector('#result').textContent.startsWith('popup fetch error:')",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup fetch completed");
        Assert.Equal("popup-token", server.LastPopupFetchToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlankPopupFetchCannotBypassStagingThroughThePrototype(bool deleteOwnOverride) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string fetch = deleteOwnOverride
            ? "delete popup.fetch; popup.fetch('/popup/fetch-result')"
            : "Object.getPrototypeOf(popup).fetch.call(popup, '/popup/fetch-result')";
        string script = $@"const popup = window.open('', '_blank');
            {fetch}.then(response => response.text()).then(text => document.querySelector('#result').textContent = text);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup fetch completed'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup fetch completed");
        Assert.Equal("popup-token", server.LastPopupFetchToken);
    }

    [Fact]
    public async Task RepeatedPopupWritesShareOneParserStream() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.document.write('<div id=""outer"">');
            popup.document.write('<span id=""inner"">nested popup write</span>');
            popup.document.write('</div>');
            const outer = popup.document.querySelector('#outer');
            const inner = popup.document.querySelector('#inner');
            document.querySelector('#result').textContent = outer?.contains(inner) ? inner.textContent : 'parser state lost';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "nested popup write");
        AssertPdfDoesNotContain(result.PdfBytes, "parser state lost");
    }

    [Fact]
    public async Task SynchronousPopupWriteDefersCssAndInlineScriptRequests() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            popup.document.write('<base href=""/popup/""><style>@import ""protected.css"";</style><script>const request = new XMLHttpRequest(); request.open(""GET"", ""script-result""); request.send();</script>');
            setInterval(() => fetch('/popup-resource-status').then(response => response.text()).then(text => {
                document.querySelector('#result').textContent = text;
            }), 20);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup resources authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.Equal("popup-token", server.LastPopupCssToken);
        Assert.Equal("popup-token", server.LastPopupScriptToken);
        AssertPdfContains(result.PdfBytes, "popup resources authorized");
    }
}
