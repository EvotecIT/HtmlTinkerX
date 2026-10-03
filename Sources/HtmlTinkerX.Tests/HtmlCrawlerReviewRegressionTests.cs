using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_ExplicitSeedRemainsUsableWithFollowUpIncludePatterns(bool render) {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context,
            "<main>Seed<a href='/docs/page'>Docs</a></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(2); options.Render = render;
        options.IncludePatterns.Add("*/docs/*");
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Equal(2, result.Pages.Count); Assert.Equal(root, result.Pages[0].Url);
    }

    [Fact]
    public async Task CrawlAsync_PreservesUserAgentAcrossAllowedOriginsWithoutSecrets() {
        string? agent = null; string? authorization = null; string? secret = null;
        using HttpListener other = StartFlexibleServer(async context => {
            agent = context.Request.UserAgent;
            authorization = context.Request.Headers["Authorization"];
            secret = context.Request.Headers["X-Crawl-Secret"];
            await RespondAsync(context, "<main>External page</main>");
        }, out string otherRoot);
        using HttpListener server = StartFlexibleServer(context => {
            context.Response.Redirect(otherRoot); return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.RestrictToHost = false;
        options.UserAgent = "HtmlTinkerX-Identity-Test";
        options.Username = "user"; options.Password = "password"; options.Headers["X-Crawl-Secret"] = "secret";
        Assert.Equal(HtmlCrawlPageStatus.Success, Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages).Status);
        Assert.Equal(options.UserAgent, agent); Assert.Null(authorization); Assert.Null(secret);
    }

    [Theory]
    [InlineData(false, "path")]
    [InlineData(true, "path")]
    [InlineData(false, "include")]
    [InlineData(true, "include")]
    [InlineData(false, "exclude")]
    [InlineData(true, "exclude")]
    public async Task CrawlAsync_FinalRedirectDestinationHonorsPageScope(bool render, string policy) {
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/docs/start") context.Response.Redirect("/private/page");
            else await RespondAsync(context, "<main>Out of scope content<a href='/private/child'>Child</a></main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1); options.Render = render;
        if (policy == "path") options.PathPrefix = "/docs/";
        if (policy == "include") options.IncludePatterns.Add("*/docs/*");
        if (policy == "exclude") options.ExcludePatterns.Add("*/private/*");
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "docs/start", options);
        Assert.Empty(result.Pages);
        HtmlCrawlPage skipped = Assert.Single(result.SkippedPages);
        Assert.Equal(200, skipped.StatusCode);
        Assert.Empty(skipped.Html); Assert.Empty(skipped.Links);
    }

    [Fact]
    public async Task CrawlAsync_ResumingFetchedOutsideHostRedirectDoesNotSpendBudgetAgain() {
        ConcurrentQueue<string> requests = new();
        using HttpListener other = StartFlexibleServer(context => RespondAsync(context, "<main>External</main>"), out string otherRoot, "127.0.0.1");
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath; requests.Enqueue(path);
            if (path == "/escape") {
                context.Response.ContentLength64 = 0;
                context.Response.Redirect(otherRoot);
            }
            else await RespondAsync(context, "<main>Root<a href='/escape'>Escape</a><a href='/remaining'>Remaining</a></main>");
        }, out string root);
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.Render = true; options.Browser = HtmlBrowserEngine.Firefox; options.OutputPath = output;
            HtmlCrawlResult first = await HtmlCrawler.CrawlAsync(root, options);
            Assert.True(first.SkippedPages.Count == 1, string.Join(";", first.Pages.Select(page => $"{page.Url}: {page.Status} {page.Error}")));
            Assert.Equal(HtmlCrawlSkipReason.OutsideHost, Assert.Single(first.SkippedPages).SkipReason);
            Assert.Single(first.PendingPages);
            int count = requests.Count;
            options.ResumePath = output;
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(count, requests.Count); Assert.Single(resumed.PendingPages);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_ComplexRobotsWildcardCannotAbortTheCrawl() {
        string pattern = "/" + string.Concat(Enumerable.Repeat("*a", 35)) + "b$";
        string path = "/" + new string('a', 150);
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context,
            context.Request.Url!.AbsolutePath == "/robots.txt" ? "User-agent: *\nDisallow: " + pattern
                : "<main>Root<a href='" + path + "'>Complex</a><a href='/safe'>Safe</a></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(3); options.RespectRobotsTxt = true;
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Contains(result.Pages, page => page.Url == root + "safe");
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"CheckpointVersion\":99}")]
    public async Task CrawlAsync_FreshOutputReplacesMalformedPriorManifestButResumeRejectsIt(string prior) {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context, "<main>Fresh</main>"), out string root);
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        string manifest = Path.Combine(output, "crawl-result.json");
        File.WriteAllText(manifest, prior);
        try {
            HtmlCrawlOptions options = StaticOptions(1); options.OutputPath = output; options.ResumePath = output;
            await Assert.ThrowsAnyAsync<Exception>(() => HtmlCrawler.CrawlAsync(root, options));
            Assert.Equal(prior, File.ReadAllText(manifest));
            options.ResumePath = null;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(HtmlCrawlPageStatus.Success, Assert.Single(result.Pages).Status);
            Assert.Single((await HtmlCrawler.LoadResultAsync(output)).Pages);
        } finally { Directory.Delete(output, true); }
    }
}
