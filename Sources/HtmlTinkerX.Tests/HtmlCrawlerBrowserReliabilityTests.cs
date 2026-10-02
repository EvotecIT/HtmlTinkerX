using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_RenderedHttpErrorsAreFailedPages() {
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.StatusCode = 404;
            await RespondAsync(context, "<main>Missing page<a href='/unexpected'>not a crawl target</a></main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.Render = true;
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(404, page.StatusCode);
        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Empty(page.Links);
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RejectsCustomHeaderRenderingWithoutPerRedirectIsolation(HtmlBrowserEngine browser) {
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true; options.Browser = browser;
        options.Headers["X-Crawl-Secret"] = "test-secret";
        NotSupportedException error = await Assert.ThrowsAsync<NotSupportedException>(() =>
            HtmlCrawler.CrawlAsync("http://localhost/", options));
        Assert.Contains("require Chromium", error.Message);
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedHeadersAndAuthenticationRemainAtStartingOrigin(HtmlBrowserEngine browser) {
        ConcurrentQueue<(string Path, string? Authorization, string? Secret)> external = new();
        using HttpListener other = StartFlexibleServer(context => {
            external.Enqueue((context.Request.RawUrl!, context.Request.Headers["Authorization"], context.Request.Headers["X-Crawl-Secret"]));
            context.Response.StatusCode = 401;
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"external\"";
            return Task.CompletedTask;
        }, out string otherRoot);
        ConcurrentQueue<(string Path, string? Authorization, string? Secret)> trusted = new();
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            trusted.Enqueue((path, context.Request.Headers["Authorization"], context.Request.Headers["X-Crawl-Secret"]));
            if (context.Request.Headers["Authorization"] == null) {
                context.Response.StatusCode = 401;
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"trusted\"";
            } else if (path == "/redirect") {
                context.Response.Redirect("/actual/page");
            } else if (path == "/redirect-asset") {
                context.Response.Redirect(otherRoot + "redirected");
            } else {
                await RespondAsync(context, $"<main><p>Rendered content</p><a href='child'>Child</a>"
                    + $"<img src='{otherRoot}direct'><img src='/redirect-asset'></main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true; options.Browser = browser;
        options.Username = "test-user"; options.Password = "test-password";
        if (browser == HtmlBrowserEngine.Chromium) options.Headers["X-Crawl-Secret"] = "test-secret";
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "redirect", options);
        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error + "\n" + string.Join(";", trusted.Select(request => $"{request.Path}: auth={request.Authorization != null}, secret={request.Secret != null}")));
        Assert.Equal(root + "actual/page", page.Url);
        Assert.Contains(root + "actual/child", page.Links);
        Assert.Contains(trusted, request => request.Path == "/actual/page" && request.Authorization != null
            && request.Secret == (browser == HtmlBrowserEngine.Chromium ? "test-secret" : null));
        Assert.NotEmpty(external);
        Assert.All(external, request => { Assert.Null(request.Authorization); Assert.Null(request.Secret); });
    }
}
