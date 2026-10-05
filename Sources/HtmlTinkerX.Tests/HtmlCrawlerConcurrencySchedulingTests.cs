using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsHonorRobotsDelayAndDisallowedPages() {
        var firstEntered = CrawlSignal();
        var secondEntered = CrawlSignal();
        int robotsRequests = 0;
        int blockedRequests = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/robots.txt":
                    Interlocked.Increment(ref robotsRequests);
                    await RespondAsync(context, "User-agent: *\nCrawl-delay: 0.4\nDisallow: /blocked");
                    break;
                case "/": await RespondAsync(context, "<a href='/one'>One</a><a href='/blocked'>Blocked</a><a href='/two'>Two</a>"); break;
                case "/one": firstEntered.TrySetResult(true); await RespondAsync(context, "<main>One</main>"); break;
                case "/two": secondEntered.TrySetResult(true); await RespondAsync(context, "<main>Two</main>"); break;
                default: Interlocked.Increment(ref blockedRequests); await RespondAsync(context, "blocked"); break;
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.RespectRobotsTxt = true;
        options.DelayMs = 50;
        Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(server.Url, options);

        await AwaitCrawlSignalAsync(firstEntered.Task);
        Assert.NotSame(secondEntered.Task, await Task.WhenAny(secondEntered.Task, Task.Delay(150)));
        HtmlCrawlResult result = await crawling;

        Assert.Equal(new[] { "/", "/one", "/two" }, result.Pages.Select(page => new Uri(page.Url).AbsolutePath));
        Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        Assert.Equal(HtmlCrawlSkipReason.DisallowedByRobots, Assert.Single(result.SkippedPages).SkipReason);
        Assert.Equal(0, Volatile.Read(ref blockedRequests));
        Assert.Equal(1, Volatile.Read(ref robotsRequests));
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsShareAnOriginRetryAfterCooldown() {
        int firstRequests = 0;
        DateTimeOffset earliest = default;
        DateTimeOffset secondArrived = default;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/": await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>"); break;
                case "/one":
                    if (Interlocked.Increment(ref firstRequests) == 1) {
                        earliest = DateTimeOffset.UtcNow.AddSeconds(1);
                        context.Response.StatusCode = 429;
                        context.Response.Headers["Retry-After"] = "1";
                        context.Response.ContentLength64 = 0;
                    } else await RespondAsync(context, "<main>Recovered one</main>");
                    break;
                default: secondArrived = DateTimeOffset.UtcNow; await RespondAsync(context, "<main>Two</main>"); break;
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 1);
        options.HttpRetryCount = 1;

        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(server.Url, options);

        Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        Assert.Equal(3, result.Pages.Count);
        Assert.Equal(2, Volatile.Read(ref firstRequests));
        Assert.True(secondArrived >= earliest, $"Another page arrived at {secondArrived:O}, before the origin cooldown {earliest:O}.");
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsDoNotBypassACooldownBeyondTheRequestDeadline() {
        int children = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>");
            else {
                Interlocked.Increment(ref children);
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = "4";
                context.Response.ContentLength64 = 0;
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 1);
        options.HttpRetryCount = 1;
        options.Timeout = 500;

        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(server.Url, options);

        Assert.Equal(HtmlCrawlPageStatus.Success, result.Pages[0].Status);
        Assert.Equal(2, result.Pages.Skip(1).Count(page => page.Status == HtmlCrawlPageStatus.Failed));
        Assert.Equal(1, Volatile.Read(ref children));
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsCountPrefetchedCanonicalDuplicatesAgainstMaxPages() {
        int children = 0;
        int unusedRequests = 0;
        var bothEntered = CrawlSignal();
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a><a href='/unused'>Unused</a>");
            else if (path == "/unused") { Interlocked.Increment(ref unusedRequests); await RespondAsync(context, "unused"); }
            else {
                if (Interlocked.Increment(ref children) == 2) bothEntered.TrySetResult(true);
                await AwaitCrawlSignalAsync(bothEntered.Task, token);
                await RespondAsync(context, "<link rel='canonical' href='/two'><main>Same destination</main>");
            }
        });

        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.UseCanonicalUrls = true;
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(server.Url, options);

        Assert.Equal(new[] { "/", "/two" }, result.Pages.Select(page => new Uri(page.Url).AbsolutePath));
        Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        Assert.Equal(2, Volatile.Read(ref children));
        Assert.Equal(0, Volatile.Read(ref unusedRequests));
        Assert.Contains(result.PendingPages, page => page.Url.EndsWith("/unused", StringComparison.Ordinal));
    }
}
