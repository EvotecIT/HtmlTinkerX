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
    public async Task OversizedStagedBeaconsReturnFalseWithoutQueuingARequest() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const accepted = popup.navigator.sendBeacon('{server.BlankPopupResourceUrl}', 'x'.repeat(64 * 1024 + 1));
            const form = new popup.FormData();
            form.append('file', new popup.File(['x'], 'n'.repeat(64 * 1024), {{ type: 'text/plain' }}));
            const formAccepted = popup.navigator.sendBeacon('{server.BlankPopupResourceUrl}?form-data', form);
            document.querySelector('#result').textContent = accepted || formAccepted ? 'oversized beacon accepted' : 'oversized beacon rejected';
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
            popup.close();
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
        string script = $@"let openerStyleOwner = HTMLElement.prototype;
            let openerStyleDescriptor;
            while (openerStyleOwner && !openerStyleDescriptor) {{ openerStyleDescriptor = Object.getOwnPropertyDescriptor(openerStyleOwner, 'style'); openerStyleOwner = Object.getPrototypeOf(openerStyleOwner); }}
            let openerSheetOwner = HTMLStyleElement.prototype;
            let openerSheetDescriptor;
            while (openerSheetOwner && !openerSheetDescriptor) {{ openerSheetDescriptor = Object.getOwnPropertyDescriptor(openerSheetOwner, 'sheet'); openerSheetOwner = Object.getPrototypeOf(openerSheetOwner); }}
            const openerAttachShadow = Element.prototype.attachShadow;
            const popup = window.open('', '_blank');
            const body = popup.document.querySelector('body');
            body.style.backgroundImage = 'url({server.BlankPopupResourceUrl}?source=cssom)';
            const legacyTable = popup.document.createElement('table');
            legacyTable.setAttribute('background', '{server.BlankPopupResourceUrl}?source=legacy-background');
            legacyTable.innerHTML = '<tr><td>legacy background</td></tr>';
            body.append(legacyTable);
            body.style.color = 'red';
            let styleOwner = Object.getPrototypeOf(body);
            let styleDescriptor;
            while (styleOwner && !styleDescriptor) {{ styleDescriptor = Object.getOwnPropertyDescriptor(styleOwner, 'style'); styleOwner = Object.getPrototypeOf(styleOwner); }}
            const nativeStyleGetter = styleDescriptor.get;
            nativeStyleGetter.call(body).borderImageSource = 'url({server.BlankPopupResourceUrl}?source=borrowed-style)';
            openerStyleDescriptor.get.call(body).maskImage = 'url({server.BlankPopupResourceUrl}?source=opener-borrowed-style)';
            const host = popup.document.createElement('div');
            body.append(host);
            const shadow = host.attachShadow({{ mode: 'open' }});
            shadow.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=shadow-markup"">';
            const shadowChild = popup.document.createElement('span');
            shadow.append(shadowChild);
            if (shadowChild.getRootNode() !== shadow || shadowChild.getRootNode().host !== host) throw new Error('shadow root identity was not preserved');
            const sheet = new popup.CSSStyleSheet();
            sheet.replaceSync(':host {{ background-image: url({server.BlankPopupResourceUrl}?source=shadow-sheet); }}');
            shadow.adoptedStyleSheets = [sheet];
            const openerHost = popup.document.createElement('div');
            body.append(openerHost);
            const openerShadow = openerAttachShadow.call(openerHost, {{ mode: 'open' }});
            openerShadow.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=opener-shadow"">';
            const style = popup.document.createElement('style');
            const nativeTextSetter = Object.getOwnPropertyDescriptor(Node.prototype, 'textContent').set;
            nativeTextSetter.call(style, '@import url({server.BlankPopupResourceUrl}?source=style-text);');
            popup.document.head.append(style);
            openerSheetDescriptor.get.call(style).insertRule('@import url({server.BlankPopupResourceUrl}?source=style-sheet);', 0);
            let replaceSyncRejected = false;
            try {{ style.sheet.replaceSync('body {{ color: red; }}'); }} catch (error) {{ replaceSyncRejected = error.name === 'NotAllowedError'; }}
            if (!replaceSyncRejected) throw new Error('non-constructed stylesheet replaceSync semantics changed');
            const dynamicScript = popup.document.createElement('script');
            nativeTextSetter.call(dynamicScript, `fetch('{server.BlankPopupResourceUrl}?source=dynamic-script')`);
            popup.document.head.append(dynamicScript);
            const externalScript = popup.document.createElement('script');
            externalScript.onload = event => {{
                if (event.target !== externalScript) throw new Error('external script identity changed');
                fetch('{server.BlankPopupResourceUrl}?source=dynamic-script-property-load');
            }};
            externalScript.addEventListener('load', () => fetch('{server.BlankPopupResourceUrl}?source=dynamic-script-listener-load'));
            externalScript.src = '{server.BlankPopupResourceUrl}?source=dynamic-external-script';
            popup.document.head.append(externalScript);
            const auxiliary = popup.document.implementation.createHTMLDocument('');
            auxiliary.body.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=auxiliary-document"">';
            popup.document.body.append(auxiliary.images[0]);
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
        Assert.True(server.BlankPopupResourceRequests >= 8);
        Assert.Equal(1, server.StyleTextResourceRequests);
        Assert.Equal(1, server.BlankPopupSourceRequests("legacy-background"));
        Assert.True(server.BlankPopupSourceRequests("style-sheet") >= 1);
        Assert.True(server.BlankPopupSourceRequests("dynamic-script") >= 1);
        Assert.Equal(1, server.BlankPopupSourceRequests("dynamic-external-script"));
        Assert.Equal(1, server.BlankPopupSourceRequests("dynamic-script-property-load"));
        Assert.Equal(1, server.BlankPopupSourceRequests("dynamic-script-listener-load"));
        Assert.True(server.BlankPopupSourceRequests("auxiliary-document") >= 1);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task BlankPopupTypedCssSrcdocAndNavigationWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const styledPopup = window.open('', '_blank');
            const body = styledPopup.document.querySelector('body');
            Object.getPrototypeOf(body.attributeStyleMap).set.call(
                body.attributeStyleMap,
                'background-image',
                'url({server.BlankPopupResourceUrl}?source=typed-om)');
            if (!body.getAttribute('style').includes('background-image')) throw new Error('typed style state lost');
            const framedPopup = window.open('', '_blank');
            const frame = framedPopup.document.createElement('iframe');
            frame.srcdoc = '<script src=""{server.BlankPopupResourceUrl}?source=srcdoc""></script>';
            framedPopup.document.body.append(frame);
            const normalizedImage = framedPopup.document.createElement('img');
            normalizedImage.src = '/blank-popup-resource?source=normalized-url';
            if (normalizedImage.src !== '{server.BlankPopupResourceUrl}?source=normalized-url') throw new Error('staged URL was not normalized');
            framedPopup.document.body.append(normalizedImage);
            let mutationTypeError = false;
            try {{ framedPopup.document.appendChild(); }} catch (error) {{ mutationTypeError = error instanceof TypeError; }}
            if (!mutationTypeError) throw new Error('invalid mutation did not fail synchronously');
            const navigatingPopup = window.open('', '_blank');
            const navigationOptions = {{ history: 'replace' }};
            Object.getPrototypeOf(navigatingPopup.navigation).navigate.call(
                navigatingPopup.navigation,
                '/blank-popup-location',
                navigationOptions);
            navigationOptions.history = 'invalid-after-call';
            const refreshPopup = window.open('', '_blank');
            const meta = refreshPopup.document.createElement('meta');
            let metaOwner = Object.getPrototypeOf(meta);
            let contentDescriptor;
            while (metaOwner && !contentDescriptor) {{ contentDescriptor = Object.getOwnPropertyDescriptor(metaOwner, 'content'); metaOwner = Object.getPrototypeOf(metaOwner); }}
            const contentSetter = contentDescriptor.set;
            contentSetter.call(meta, '0;url=/blank-popup-location');
            meta.httpEquiv = 'refresh';
            refreshPopup.document.head.append(meta);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                delayMilliseconds: 1000,
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.True(server.BlankPopupResourceRequests >= 2);
        Assert.Equal(1, server.BlankPopupSourceRequests("normalized-url"));
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
            let invalidSetterCaught = false;
            try { popup.location.href = 'http://['; } catch { invalidSetterCaught = true; }
            const source = new popup.EventSource('/popup/events');
            source.close();
            globalThis.popupSynchronousState = invalidLocationCaught
                && invalidSetterCaught
                && source.CONNECTING === popup.EventSource.CONNECTING
                && source.CLOSED === popup.EventSource.CLOSED
                && source.readyState === source.CLOSED;
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

    [Fact]
    public async Task BlankPopupTimersRunOnlyAfterHeaderInterceptionIsReady() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const openerSetTimeout = Window.prototype.setTimeout;
            const popup = window.open('', '_blank');
            const cancelled = popup.setTimeout(() => popup.document.body.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=cancelled-timer"">', 0);
            popup.clearTimeout(String(cancelled));
            const delayed = popup.setTimeout(() => popup.document.body.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=late-cancelled-timer"">', 100);
            popup.setTimeout(() => popup.clearTimeout(delayed), 0);
            openerSetTimeout.call(popup, () => popup.document.body.append(Object.assign(popup.document.createElement('img'), {{ src: '{server.BlankPopupResourceUrl}?source=opener-timer' }})), 0);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupResourceRequests);
        Assert.Equal(0, server.BlankPopupSourceRequests("late-cancelled-timer"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task ChildFrameDocumentsAndAncestorsStayBehindPopupStaging() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            frame.contentDocument.body.innerHTML = '<img src=""{server.BlankPopupResourceUrl}?source=child-document-realm"">';
            const parentImage = frame.contentWindow.parent.document.createElement('img');
            parentImage.src = '{server.BlankPopupResourceUrl}?source=child-parent-window';
            frame.contentWindow.top.document.querySelector('body').appendChild(parentImage);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("child-document-realm"));
        Assert.Equal(1, server.BlankPopupSourceRequests("child-parent-window"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task SynchronousPopupXhrIsRejectedBeforeItCanAppearDeferred() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            let rejected = false;
            let defaultedAsync = false;
            try { const request = new popup.XMLHttpRequest(); request.open('GET', '/blank-popup-resource', undefined); request.abort(); defaultedAsync = true; }
            catch { defaultedAsync = false; }
            try { const request = new popup.XMLHttpRequest(); request.open('GET', '/blank-popup-resource', false); }
            catch (error) { rejected = error.name === 'NotSupportedError'; }
            document.querySelector('#result').textContent = rejected && defaultedAsync ? 'sync xhr rejected' : 'sync xhr deferred';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "sync xhr rejected");
        Assert.Equal(0, server.BlankPopupResourceRequests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task DynamicPopupScriptTextWaitsForHeaderInterception(int setter) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string scriptText = $"fetch('{server.BlankPopupResourceUrl}?source=dynamic-script-{setter}')";
        string assignment = setter switch {
            0 => $"dynamic.text = {System.Text.Json.JsonSerializer.Serialize(scriptText)};",
            1 => $"dynamic.innerHTML = {System.Text.Json.JsonSerializer.Serialize(scriptText)};",
            2 => $"dynamic.appendChild(popup.document.createTextNode({System.Text.Json.JsonSerializer.Serialize(scriptText)}));",
            3 => $"dynamic.innerText = {System.Text.Json.JsonSerializer.Serialize(scriptText)};",
            _ => $"dynamic.append({System.Text.Json.JsonSerializer.Serialize(scriptText)});"
        };
        string script = $@"const popup = window.open('', '_blank');
            const dynamic = popup.document.createElement('script');
            {assignment}
            popup.document.querySelector('head').appendChild(dynamic);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests($"dynamic-script-{setter}"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupRealmCodeExecutionWaitsForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            popup.eval(`fetch('{server.BlankPopupResourceUrl}?source=popup-eval')`);
            new popup.Function(`fetch('{server.BlankPopupResourceUrl}?source=popup-function')`)();
            popup.queueMicrotask(() => popup.fetch('{server.BlankPopupResourceUrl}?source=popup-microtask'));
            popup.requestAnimationFrame(() => popup.fetch('{server.BlankPopupResourceUrl}?source=popup-animation'));
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            frame.contentWindow.eval(`fetch('{server.BlankPopupResourceUrl}?source=frame-eval')`);
            frame.contentWindow.setTimeout(`fetch('{server.BlankPopupResourceUrl}?source=frame-timer')`, 0);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        foreach (string source in new[] { "popup-eval", "popup-function", "popup-microtask", "popup-animation", "frame-eval", "frame-timer" }) {
            Assert.Equal(1, server.BlankPopupSourceRequests(source));
        }
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupFontFaceLoadsOnlyAfterHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const direct = new popup.FontFace('direct', `url('{server.BlankPopupResourceUrl}?source=font-load')`);
            direct.load().catch(() => undefined);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("font-load"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }
}
