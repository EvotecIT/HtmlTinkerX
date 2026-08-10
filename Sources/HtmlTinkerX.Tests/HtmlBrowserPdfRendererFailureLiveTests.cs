using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task CallerScriptFailureThatResemblesATransportErrorDoesNotReplayCapture() {
        await using LoopbackContentServer origin = new("<html><body><p>run once</p></body></html>");
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        await Assert.ThrowsAsync<PlaywrightException>(() => renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(origin.Url),
            beforeCaptureScript: "throw new Error('target page, context or browser has been closed');")));

        HtmlBrowserPdfRendererMetrics failed = renderer.GetMetricsSnapshot();
        Assert.Equal(1, origin.RequestCount);
        Assert.Equal(0, failed.BrowserFailureRetries);
        Assert.Equal(0, failed.BrowsersRecycled);
        Assert.Equal(1, failed.BrowsersCreated);

        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>healthy browser reused</p></body></html>")));

        AssertPdfContains(recovered.PdfBytes, "healthy browser reused");
        Assert.True(recovered.Diagnostics.BrowserReused);
        Assert.Equal(1, renderer.GetMetricsSnapshot().BrowsersCreated);
    }
}
