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
            const frame = popup.document.createElement('iframe');
            frame.dataset.identity = 'retained';
            frame.addEventListener('load', () => {{
                document.querySelector('#result').textContent = frame.dataset.identity;
                popup.close();
            }});
            frame.src = '{server.BlankPopupResourceUrl}';
            popup.document.body.appendChild(frame);
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
}
