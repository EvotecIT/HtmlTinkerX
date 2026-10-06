using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium, HtmlCrawlHiddenContentMode.RespectHidden)]
    [InlineData(HtmlBrowserEngine.Firefox, HtmlCrawlHiddenContentMode.RespectHidden)]
    [InlineData(HtmlBrowserEngine.WebKit, HtmlCrawlHiddenContentMode.RespectHidden)]
    [InlineData(HtmlBrowserEngine.Chromium, HtmlCrawlHiddenContentMode.IncludeHidden)]
    [InlineData(HtmlBrowserEngine.Firefox, HtmlCrawlHiddenContentMode.IncludeHidden)]
    [InlineData(HtmlBrowserEngine.WebKit, HtmlCrawlHiddenContentMode.IncludeHidden)]
    public async Task CrawlAsync_DocumentCaptureHonorsComputedHiddenContent(HtmlBrowserEngine browser, HtmlCrawlHiddenContentMode mode) {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context,
            "<style>.theme-hidden { display: none; }</style><main><p>Visible document</p><p class='theme-hidden'>Hidden stylesheet text</p></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.HiddenContentMode = mode;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Contains("Visible document", page.Text);
        Assert.Equal(mode == HtmlCrawlHiddenContentMode.IncludeHidden, page.Text.Contains("Hidden stylesheet text"));
        Assert.Equal(root, page.ResponseUrl);
    }
}
