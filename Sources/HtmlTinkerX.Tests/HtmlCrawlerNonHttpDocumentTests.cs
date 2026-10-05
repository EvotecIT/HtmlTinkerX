using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedNonHttpDocumentDoesNotRetainPreviousResponseMetadata(HtmlBrowserEngine browser) {
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"initial\"";
            context.Response.Headers["Last-Modified"] = "Sat, 03 Oct 2026 12:00:00 GMT";
            await RespondAsync(context, "<main>Initial document</main><a id='continue' href='about:blank'>Continue</a>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.ClickSelectors.Add("#continue");
        options.WaitAfterLoadMs = 100;
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).SkippedPages);
        Assert.Equal(HtmlCrawlSkipReason.InvalidUrl, page.SkipReason);
        Assert.Equal("about:blank", page.Url);
        Assert.Null(page.ResponseUrl);
        Assert.Null(page.EntityTag);
        Assert.Null(page.LastModified);
        Assert.Null(page.StatusCode);
        Assert.Null(page.ContentType);
    }
}
