using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DeclarativeSameOriginPopupsWaitForOriginScopedHeaderInterception(int submissionMode) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(submissionMode == 0 ? server.DeclarativeAnchorUrl : server.DeclarativeFormUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: submissionMode switch {
                1 => "document.querySelector('form').requestSubmit(); true",
                2 => "document.querySelector('form').submit(); true",
                _ => "document.querySelector('a').click(); true"
            }));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }
}
