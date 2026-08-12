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
    public async Task RepeatedNamedWindowOpenReusesTheGuardedPopup() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const first = window.open('', 'report');
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
