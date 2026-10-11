using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrawlAsync_ConcurrentDownloadsRejectReusableResponseCaches(bool storeResponses) {
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        if (storeResponses) options.CacheResponses = true;
        else options.RefreshPath = Path.Combine(Path.GetTempPath(), "unused-refresh-" + Guid.NewGuid().ToString("N"));

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() => HtmlCrawler.CrawlAsync("https://example.test/", options));

        Assert.Equal(nameof(HtmlCrawlOptions.MaxConcurrentPageRequests), error.ParamName);
        Assert.Contains("ordered cookie state", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(503)]
    public async Task CrawlAsync_ConcurrentDownloadsPreserveFailedDuplicateAccountingOnResume(int status) {
        var bothEntered = CrawlSignal();
        int children = 0;
        int unusedRequests = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a><a href='/unused'>Unused</a>");
            else if (path == "/unused") { Interlocked.Increment(ref unusedRequests); await RespondAsync(context, "unused"); }
            else {
                if (Interlocked.Increment(ref children) == 2) bothEntered.TrySetResult(true);
                await AwaitCrawlSignalAsync(bothEntered.Task, token);
                if (path == "/one") await RespondAsync(context, "<link rel='canonical' href='/two'><main>Canonical page</main>");
                else if (status == 0) await Task.Delay(Timeout.Infinite, token);
                else { context.Response.StatusCode = status; context.Response.ContentLength64 = 0; }
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.UseCanonicalUrls = true;
        options.Timeout = 1000;
        string output = Path.Combine(Path.GetTempPath(), "HtmlCrawlerConcurrencyFailedResume", Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(server.Url, options);
            HtmlCrawlPage failed = Assert.Single(result.Pages, page => page.Status == HtmlCrawlPageStatus.Failed);
            Assert.Equal(status == 0 ? (int?)null : status, failed.StatusCode);
            Assert.Equal(3, result.Pages.Count);
            await HtmlCrawler.SaveResultAsync(result, output);
            HtmlCrawlOptions resume = options.Clone();
            resume.ResumePath = output;
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(server.Url, resume);
            Assert.Equal(3, resumed.Pages.Count);
            Assert.Single(resumed.Pages, page => page.Status == HtmlCrawlPageStatus.Failed);
            Assert.Equal(0, Volatile.Read(ref unusedRequests));
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsCancelWhileRedirectRobotsHoldsTheGate() {
        var robotsEntered = CrawlSignal();
        var releaseRobots = CrawlSignal();
        await using ConcurrentCrawlServer destination = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/robots.txt") {
                robotsEntered.TrySetResult(true);
                await SendHeldCrawlBodyAsync(context, "User-agent: *\nAllow: /", releaseRobots.Task, token);
            } else await RespondAsync(context, "<main>Redirected page</main>");
        });
        await using ConcurrentCrawlServer origin = new(async (context, token) => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/robots.txt": await RespondAsync(context, "User-agent: *\nAllow: /"); break;
                case "/": await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>"); break;
                case "/one":
                    context.Response.StatusCode = 302;
                    context.Response.RedirectLocation = destination.Url + "page";
                    context.Response.ContentLength64 = 0;
                    break;
                default: await RespondAsync(context, "<main>Other page</main>"); break;
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.RespectRobotsTxt = true;
        using CancellationTokenSource cancellation = new();
        Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(origin.Url, options, cancellation.Token);
        try {
            // The destination robots body holds the same gate used by coordinator checks.
            await AwaitCrawlSignalAsync(robotsEntered.Task);
            cancellation.Cancel();
            Assert.Same(crawling, await Task.WhenAny(crawling, Task.Delay(5000)));
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => crawling);
            Assert.Equal(cancellation.Token, error.CancellationToken);
        } finally { releaseRobots.TrySetResult(true); }
    }
}
