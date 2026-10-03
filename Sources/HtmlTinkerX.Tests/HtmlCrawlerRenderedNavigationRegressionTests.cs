using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData("path")]
    [InlineData("include")]
    [InlineData("exclude")]
    public async Task CrawlAsync_RenderedInteractionNavigationHonorsFinalPageScope(string policy) {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context,
            context.Request.Url!.AbsolutePath == "/docs/start" ? "<a id='leave' href='/private/page'>Leave</a>"
                : "<main>Private content<a href='child'>Child</a></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.Render = true; options.ClickSelectors.Add("#leave");
        if (policy == "path") options.PathPrefix = "/docs/";
        if (policy == "include") options.IncludePatterns.Add("*/docs/*");
        if (policy == "exclude") options.ExcludePatterns.Add("*/private/*");
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "docs/start", options);
        Assert.Empty(result.Pages);
        HtmlCrawlPage skipped = Assert.Single(result.SkippedPages);
        Assert.Equal(root + "private/page", skipped.Url); Assert.Empty(skipped.Html); Assert.Empty(skipped.Links);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    public async Task CrawlAsync_RenderedInteractionUsesFinalUrlAndResponse(int status) {
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/docs/start") await RespondAsync(context, "<a id='leave' href='/docs/actual/page'>Leave</a>");
            else {
                context.Response.StatusCode = status;
                await RespondAsync(context, "<title>Final document</title><main>Final<a href='child'>Child</a><img src='asset.png'></main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.Render = true; options.ClickSelectors.Add("#leave"); options.PathPrefix = "/docs/";
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root + "docs/start", options)).Pages);
        Assert.Equal(root + "docs/actual/page", page.Url); Assert.Equal(status, page.StatusCode);
        Assert.Equal(status == 200 ? HtmlCrawlPageStatus.Success : HtmlCrawlPageStatus.Failed, page.Status);
        if (status == 200) {
            Assert.Equal("Final document", page.Title);
            Assert.Contains(root + "docs/actual/child", page.Links);
            Assert.Contains(root + "docs/actual/asset.png", page.AssetUrls);
        } else { Assert.Empty(page.Html); Assert.Empty(page.Links); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_FinalRenderedFetchPropagatesCallerCancellation(bool duringInteraction) {
        using CancellationTokenSource cancellation = new();
        using HttpListener server = StartFlexibleServer(async context => {
            if (!duringInteraction || context.Request.Url!.AbsolutePath == "/cancel") cancellation.Cancel();
            await RespondAsync(context, "<main>Content<button id='cancel' onclick=\"fetch('/cancel')\">Cancel</button></main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.Render = true; options.MaxDepth = 0;
        if (duringInteraction) { options.ClickSelectors.Add("#cancel"); options.InteractionDelayMs = 1000; }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlCrawler.CrawlAsync(root, options, cancellation.Token));
    }
}
