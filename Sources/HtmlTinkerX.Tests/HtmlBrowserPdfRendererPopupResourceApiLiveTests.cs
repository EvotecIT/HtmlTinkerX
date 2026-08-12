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
    public async Task ChildFrameNodesFetchAndBeaconUseTheChildDocumentStagingRealm() {
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
            frame.contentWindow.navigator.sendBeacon('', 'audit');
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
        Assert.Equal(2, server.BlankPopupSourceRequests("child-base-fetch"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task ChildFrameResourceApisResolveAgainstTheChildStagedBase() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            const base = frame.contentDocument.createElement('base');
            base.href = '{server.BlankPopupResourceUrl}?source=child-api-base';
            frame.contentDocument.head.append(base);
            const request = new frame.contentWindow.XMLHttpRequest();
            request.open('GET', '');
            request.send();
            frame.contentWindow.caches.open('htmltinkerx-child-base').then(cache => cache.add(''));
            new frame.contentWindow.Worker('');
            const events = new frame.contentWindow.EventSource('');
            events.onerror = () => events.close();
            frame.contentWindow.fetch(new frame.contentWindow.Request(''));
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1500),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.NotEmpty(result.PdfBytes);
        Assert.True(server.BlankPopupSourceRequests("child-api-base") >= 5);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task UnsafeMarkupAndMediaPlaybackWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            let missingMarkupRejected = false;
            try {{ popup.document.body.setHTMLUnsafe(); }} catch (error) {{ missingMarkupRejected = error.name === 'TypeError'; }}
            popup.document.body.setHTMLUnsafe(`<div id='unsafe-host'><img src='{server.BlankPopupResourceUrl}?source=unsafe-element'></div>`);
            const shadow = popup.document.querySelector('#unsafe-host').attachShadow({{ mode: 'open' }});
            shadow.setHTMLUnsafe(`<img src='{server.BlankPopupResourceUrl}?source=unsafe-shadow'>`);
            const frame = popup.document.createElement('iframe');
            popup.document.body.append(frame);
            void frame.contentWindow;
            const childHost = frame.contentDocument.createElement('div');
            frame.contentDocument.body.append(childHost);
            childHost.attachShadow({{ mode: 'open' }}).setHTMLUnsafe(`<img src='{server.BlankPopupResourceUrl}?source=unsafe-child-shadow'>`);
            const audio = frame.contentDocument.createElement('audio');
            audio.src = '{server.BlankPopupResourceUrl}?source=media-play';
            frame.contentDocument.body.append(audio);
            audio.play().then(
                () => document.querySelector('#result').textContent = missingMarkupRejected ? 'media playback settled' : 'markup validation lost',
                () => document.querySelector('#result').textContent = missingMarkupRejected ? 'media playback settled' : 'markup validation lost');
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'media playback settled'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "media playback settled");
        Assert.Equal(1, server.BlankPopupSourceRequests("unsafe-element"));
        Assert.Equal(1, server.BlankPopupSourceRequests("unsafe-shadow"));
        Assert.Equal(1, server.BlankPopupSourceRequests("unsafe-child-shadow"));
        Assert.Equal(1, server.BlankPopupSourceRequests("media-play"));
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
    }

    [Fact]
    public async Task AdoptedNodesParsedResourcesAndCookiesWaitForHeaderInterception() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = $@"const popup = window.open('', '_blank');
            const body = popup.document.querySelector('body');
            const openerImage = document.createElement('img');
            Node.prototype.appendChild.call(body, openerImage);
            Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src').set.call(
                openerImage,
                '{server.BlankPopupResourceUrl}?source=adopted-opener-node');
            const appendedImage = document.createElement('img');
            Element.prototype.append.call(body, appendedImage);
            Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src').set.call(
                appendedImage,
                '{server.BlankPopupResourceUrl}?source=adopted-opener-append');
            const parsed = new popup.DOMParser().parseFromString(
                `<img src='{server.BlankPopupResourceUrl}?source=popup-dom-parser'>`,
                'text/html');
            body.append(parsed.images[0]);
            const borrowedParser = new popup.DOMParser();
            const borrowed = DOMParser.prototype.parseFromString.call(
                borrowedParser,
                `<img src='{server.BlankPopupResourceUrl}?source=borrowed-dom-parser'>`,
                'text/html');
            body.append(borrowed.images[0]);
            const frame = popup.document.createElement('iframe');
            body.append(frame);
            const childParsed = new frame.contentWindow.DOMParser().parseFromString(
                `<img src='{server.BlankPopupResourceUrl}?source=child-dom-parser'>`,
                'text/html');
            body.append(childParsed.images[0]);
            popup.document.cookie = 'stage=ready; path=/';
            const external = popup.document.createElement('script');
            external.src = '{server.BlankPopupResourceUrl}?source=cookie-script';
            body.append(external);
            document.querySelector('#result').textContent = 'popup DOM staged';
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup DOM staged");
        foreach (string source in new[] { "adopted-opener-node", "adopted-opener-append", "popup-dom-parser", "borrowed-dom-parser", "child-dom-parser", "cookie-script" }) {
            Assert.Equal(1, server.BlankPopupSourceRequests(source));
        }
        Assert.Contains("stage=ready", server.BlankPopupSourceCookie("cookie-script"), StringComparison.Ordinal);
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
            const base = popup.document.createElement('base');
            base.href = '/popup/';
            popup.document.head.append(base);
            const worker = new popup.Worker('worker.js');
            const source = new popup.EventSource('events');
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
