using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task BlankPopupSelfAliasesRemainStagedBehindThePrivateReleaseHandshake() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = @"const popup = window.open('', '_blank');
            for (const name of Object.getOwnPropertyNames(popup)) {
                if (name.toLowerCase().includes('htmltinkerx') && typeof popup[name] === 'function') popup[name]();
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
            const image = popup.document.createElement('img');
            delete image.src;
            image.src = '{server.BlankPopupResourceUrl}';
            true";

        await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 750),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        Assert.True(server.BlankPopupResourceRequests > 0);
        Assert.Equal(0, server.UnauthorizedBlankPopupResourceRequests);
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
