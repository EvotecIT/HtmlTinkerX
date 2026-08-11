using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
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
}
