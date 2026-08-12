using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task PopupFontFaceSetLoadsOnlyAfterHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const direct = new popup.FontFace('direct-set', `url('{server.BlankPopupResourceUrl}?source=font-set-load')`);
            const borrowed = new popup.FontFace('borrowed-set', `url('{server.BlankPopupResourceUrl}?source=font-set-borrowed')`);
            popup.document.fonts.add(direct).add(borrowed);
            const openerLoad = Object.getPrototypeOf(document.fonts).load;
            Promise.all([
                popup.document.fonts.load('12px direct-set'),
                openerLoad.call(popup.document.fonts, '12px borrowed-set')
            ]).catch(() => undefined);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("font-set-load"));
        Assert.Equal(1, server.BlankPopupSourceRequests("font-set-borrowed"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupImageDecodeWaitsForStagedSourceRestoration() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const direct = popup.document.createElement('img');
            direct.src = '{server.BlankPopupResourceUrl}?source=image-decode';
            const borrowed = popup.document.createElement('img');
            borrowed.src = '{server.BlankPopupResourceUrl}?source=image-decode-borrowed';
            const openerDecode = HTMLImageElement.prototype.decode;
            Promise.all([direct.decode(), openerDecode.call(borrowed)]).then(() => {{
                document.querySelector('#result').textContent = 'images decoded';
            }});
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'images decoded'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "images decoded");
        Assert.Equal(1, server.BlankPopupSourceRequests("image-decode"));
        Assert.Equal(1, server.BlankPopupSourceRequests("image-decode-borrowed"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task PopupCacheAddsWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const openerOpen = Object.getPrototypeOf(caches).open;
            Promise.all([
                popup.caches.open('htmltinkerx-popup'),
                openerOpen.call(popup.caches, 'htmltinkerx-popup-borrowed')
            ]).then(([cache, borrowedCache]) => {{
                const openerAdd = Cache.prototype.add;
                return Promise.all([
                    cache.add('{server.BlankPopupResourceUrl}?source=cache-add'),
                    openerAdd.call(borrowedCache, '{server.BlankPopupResourceUrl}?source=cache-add-borrowed'),
                    cache.addAll(['{server.BlankPopupResourceUrl}?source=cache-add-all'])
                ]);
            }}).then(() => document.querySelector('#result').textContent = 'cache populated');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'cache populated'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "cache populated");
        Assert.Equal(1, server.BlankPopupSourceRequests("cache-add"));
        Assert.Equal(1, server.BlankPopupSourceRequests("cache-add-borrowed"));
        Assert.Equal(1, server.BlankPopupSourceRequests("cache-add-all"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task ChildFrameCacheAndFontFaceSetWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            const direct = new popup.FontFace('child-direct', `url('{server.BlankPopupResourceUrl}?source=child-font-set')`);
            const borrowed = new popup.FontFace('child-borrowed', `url('{server.BlankPopupResourceUrl}?source=child-font-set-borrowed')`);
            frame.contentDocument.fonts.add(direct).add(borrowed);
            const openerFontLoad = Object.getPrototypeOf(document.fonts).load;
            const openerCacheOpen = Object.getPrototypeOf(caches).open;
            Promise.allSettled([
                frame.contentDocument.fonts.load('12px child-direct'),
                openerFontLoad.call(frame.contentDocument.fonts, '12px child-borrowed'),
                frame.contentWindow.caches.open('htmltinkerx-child').then(cache => cache.add('{server.BlankPopupResourceUrl}?source=child-cache')),
                openerCacheOpen.call(frame.contentWindow.caches, 'htmltinkerx-child-borrowed').then(cache => Cache.prototype.add.call(cache, '{server.BlankPopupResourceUrl}?source=child-cache-borrowed'))
            ]).then(() => document.querySelector('#result').textContent = 'child resources staged');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'child resources staged'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "child resources staged");
        Assert.Equal(1, server.BlankPopupSourceRequests("child-font-set"));
        Assert.Equal(1, server.BlankPopupSourceRequests("child-font-set-borrowed"));
        Assert.Equal(1, server.BlankPopupSourceRequests("child-cache"));
        Assert.Equal(1, server.BlankPopupSourceRequests("child-cache-borrowed"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task ChildFrameNodesAndFetchUseTheChildDocumentStagingRealm() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            const base = frame.contentDocument.createElement('base');
            base.href = '{server.BlankPopupResourceUrl}?source=child-base-fetch';
            frame.contentDocument.head.appendChild(base);
            frame.contentDocument.body.innerHTML = `<img src='{server.BlankPopupResourceUrl}?source=child-realm-node'>`;
            frame.contentWindow.fetch('').then(() => document.querySelector('#result').textContent = 'child realm staged');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'child realm staged'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "child realm staged");
        Assert.Equal(1, server.BlankPopupSourceRequests("child-realm-node"));
        Assert.Equal(1, server.BlankPopupSourceRequests("child-base-fetch"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task AudioConstructorAndSvgAnimatedHrefWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const audio = new popup.Audio('{server.BlankPopupResourceUrl}?source=audio-constructor');
            popup.document.body.append(audio);
            const image = popup.document.createElementNS('http://www.w3.org/2000/svg', 'image');
            popup.document.body.append(image);
            image.href.baseVal = '{server.BlankPopupResourceUrl}?source=image-decode-svg';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(1, server.BlankPopupSourceRequests("audio-constructor"));
        Assert.Equal(1, server.BlankPopupSourceRequests("image-decode-svg"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task DeferredWorkerAndEventSourcePreserveExpandosAfterActivation() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const popup = window.open('', '_blank');
            const worker = new popup.Worker('/popup/worker.js');
            const source = new popup.EventSource('/popup/events');
            worker.jobId = 'worker-report';
            source.jobId = 'event-report';
            let workerOk = false;
            let sourceOk = false;
            const complete = () => {
                if (workerOk && sourceOk) document.querySelector('#result').textContent = 'expandos preserved';
            };
            worker.onmessage = () => { workerOk = worker.jobId === 'worker-report'; complete(); };
            source.onmessage = () => { sourceOk = source.jobId === 'event-report'; source.close(); complete(); };
            worker.postMessage('start');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'expandos preserved'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "expandos preserved");
        Assert.Equal("popup-token", server.LastPopupWorkerToken);
        Assert.Equal("popup-token", server.LastPopupEventToken);
    }

    [Fact]
    public async Task RepeatedNamedWindowOpenReusesTheGuardedPopup() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const first = window.open('', '_blank');
            first.name = 'report';
            const second = window.open('/blank-popup-location', 'report');
            if (first !== second) throw new Error('named popup facade was not reused');
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
}
