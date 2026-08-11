using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPdfRendererSecurityContractTests {
    [Fact]
    public async Task RendererOwnedContextsRejectDownloads() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions());
        HtmlBrowserPdfRequest request = new(HtmlBrowserPdfSource.FromHtml("<p>download boundary</p>"));

        BrowserNewContextOptions options = renderer.CreateContextOptions(request);

        Assert.False(options.AcceptDownloads);
    }
}
