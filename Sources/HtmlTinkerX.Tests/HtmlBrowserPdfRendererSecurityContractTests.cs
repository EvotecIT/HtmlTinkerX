using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPdfRendererSecurityContractTests {
    [Fact]
    public void BrowserFailureReplayRequiresAnExplicitIdempotencyOptIn() {
        HtmlBrowserPdfSource source = HtmlBrowserPdfSource.FromHtml("<p>side effect boundary</p>");
        HtmlBrowserPdfRequest defaultRequest = new(source);
        HtmlBrowserPdfRequest idempotentRequest = new(source, retryOnBrowserFailure: true);

        Assert.False(defaultRequest.RetryOnBrowserFailure);
        Assert.False(HtmlBrowserPdfRenderer.CanRetryBrowserFailure(defaultRequest, 0, false, true));
        Assert.True(HtmlBrowserPdfRenderer.CanRetryBrowserFailure(idempotentRequest, 0, false, true));
        Assert.False(HtmlBrowserPdfRenderer.CanRetryBrowserFailure(idempotentRequest, 1, false, true));
        Assert.False(HtmlBrowserPdfRenderer.CanRetryBrowserFailure(idempotentRequest, 0, true, true));
        Assert.False(HtmlBrowserPdfRenderer.CanRetryBrowserFailure(idempotentRequest, 0, false, false));
    }

    [Fact]
    public async Task RendererOwnedContextsRejectDownloads() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions());
        HtmlBrowserPdfRequest request = new(HtmlBrowserPdfSource.FromHtml("<p>download boundary</p>"));

        BrowserNewContextOptions options = renderer.CreateContextOptions(request);

        Assert.False(options.AcceptDownloads);
    }
}
