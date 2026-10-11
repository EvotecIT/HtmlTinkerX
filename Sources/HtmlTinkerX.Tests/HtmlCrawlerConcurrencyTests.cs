using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_ConcurrentDownloadsPreserveDiscoveryOrderAndPendingCheckpoints() {
        var slowEntered = CrawlSignal();
        var fastSent = CrawlSignal();
        var releaseSlow = CrawlSignal();
        int unusedRequests = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/": await RespondAsync(context, "<a href='/slow'>Slow</a><a href='/fast'>Fast</a><a href='/unused'>Unused</a>"); break;
                case "/slow": slowEntered.TrySetResult(true); await SendHeldCrawlBodyAsync(context, "<main>Slow page</main>", releaseSlow.Task, token); break;
                case "/fast": await AwaitCrawlSignalAsync(slowEntered.Task, token); await RespondAsync(context, "<main>Fast page</main>"); fastSent.TrySetResult(true); break;
                default: Interlocked.Increment(ref unusedRequests); await RespondAsync(context, "unused"); break;
            }
        });
        string output = Path.Combine(Path.GetTempPath(), "HtmlCrawlerConcurrency", Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = ConcurrentOptions(3, 2);
            options.OutputPath = output;
            Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(server.Url, options);
            await AwaitCrawlSignalAsync(fastSent.Task);
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(output);
            Assert.Single(checkpoint.Pages);
            Assert.Contains(checkpoint.PendingPages, item => item.Url.EndsWith("/slow", StringComparison.Ordinal));
            Assert.Contains(checkpoint.PendingPages, item => item.Url.EndsWith("/fast", StringComparison.Ordinal));
            releaseSlow.TrySetResult(true);
            HtmlCrawlResult result = await crawling;
            Assert.Equal(new[] { "/", "/slow", "/fast" }, result.Pages.Select(page => new Uri(page.Url).AbsolutePath));
            Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
            Assert.Contains("Fast page", result.Pages[2].Text);
            Assert.Equal(0, Volatile.Read(ref unusedRequests));
        } finally {
            releaseSlow.TrySetResult(true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentObserverReceivesContentBeforeItIsReleased() {
        var slowEntered = CrawlSignal();
        var fastSent = CrawlSignal();
        var releaseSlow = CrawlSignal();
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/": await RespondAsync(context, "<a href='/slow'>Slow</a><a href='/fast'>Fast</a>"); break;
                case "/slow":
                    slowEntered.TrySetResult(true);
                    await SendHeldCrawlBodyAsync(context, "<main>Slow body</main>", releaseSlow.Task, token);
                    break;
                default:
                    await AwaitCrawlSignalAsync(slowEntered.Task, token);
                    await RespondAsync(context, "<main>Fast body</main>");
                    fastSent.TrySetResult(true);
                    break;
            }
        });
        string output = Path.Combine(Path.GetTempPath(), "HtmlCrawlerConcurrency", Guid.NewGuid().ToString("N"));
        List<HtmlCrawlPage> observed = new();
        try {
            HtmlCrawlOptions options = ConcurrentOptions(3, 2);
            options.OutputPath = output;
            options.RetainPageContent = false;
            options.PageObserver = new DelegatePageObserver((page, token) => {
                observed.Add(page.CreateSnapshot());
                return Task.CompletedTask;
            });
            Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(server.Url, options);
            await AwaitCrawlSignalAsync(fastSent.Task);
            releaseSlow.TrySetResult(true);
            HtmlCrawlResult result = await crawling;
            Assert.Equal(new[] { "/", "/slow", "/fast" }, observed.Select(page => new Uri(page.Url).AbsolutePath));
            Assert.Contains("Slow body", observed[1].Text);
            Assert.Contains("Fast body", observed[2].Text);
            Assert.All(result.Pages, page => Assert.Empty(page.Text));
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(output);
            Assert.Contains("Slow body", loaded.Pages[1].Text);
            Assert.Contains("Fast body", loaded.Pages[2].Text);
        } finally {
            releaseSlow.TrySetResult(true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task CrawlAsync_OriginSlotRemainsHeldUntilTheResponseBodyCloses() {
        var slowEntered = CrawlSignal();
        var fastEntered = CrawlSignal();
        var releaseSlow = CrawlSignal();
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/") await RespondAsync(context, "<a href='/slow'>Slow</a><a href='/fast'>Fast</a>");
            else if (context.Request.Url.AbsolutePath == "/slow") {
                slowEntered.TrySetResult(true);
                await SendHeldCrawlBodyAsync(context, "<main>Slow</main>", releaseSlow.Task, token);
            } else { fastEntered.TrySetResult(true); await RespondAsync(context, "<main>Fast</main>"); }
        });
        Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(server.Url, ConcurrentOptions(3, 1));
        try {
            await AwaitCrawlSignalAsync(slowEntered.Task);
            Assert.NotSame(fastEntered.Task, await Task.WhenAny(fastEntered.Task, Task.Delay(250)));
            releaseSlow.TrySetResult(true);
            HtmlCrawlResult result = await crawling;
            Assert.Equal(3, result.Pages.Count);
            Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        } finally { releaseSlow.TrySetResult(true); }
    }

    [Fact]
    public async Task CrawlAsync_RedirectedDownloadsUseTheDestinationOriginLimit() {
        var slowEntered = CrawlSignal();
        var fastEntered = CrawlSignal();
        var releaseSlow = CrawlSignal();
        await using ConcurrentCrawlServer destination = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/slow") {
                slowEntered.TrySetResult(true);
                await SendHeldCrawlBodyAsync(context, "<main>Redirected slow</main>", releaseSlow.Task, token);
            } else { fastEntered.TrySetResult(true); await RespondAsync(context, "<main>Redirected fast</main>"); }
        });
        await using ConcurrentCrawlServer origin = new(async (context, token) => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>");
            else {
                if (path == "/two") await AwaitCrawlSignalAsync(slowEntered.Task, token);
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = destination.Url + (path == "/one" ? "slow" : "fast");
                context.Response.ContentLength64 = 0;
            }
        });
        Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(origin.Url, ConcurrentOptions(3, 1));
        try {
            await AwaitCrawlSignalAsync(slowEntered.Task);
            Assert.NotSame(fastEntered.Task, await Task.WhenAny(fastEntered.Task, Task.Delay(250)));
            releaseSlow.TrySetResult(true);
            HtmlCrawlResult result = await crawling;
            Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
            Assert.Equal(new[] { "/", "/slow", "/fast" }, result.Pages.Select(page => new Uri(page.Url).AbsolutePath));
        } finally { releaseSlow.TrySetResult(true); }
    }

    [Fact]
    public async Task CrawlAsync_ConcurrentResponsesShareTheAggregateByteProbe() {
        const string rootBody = "<a href='/one'>One</a><a href='/two'>Two</a>";
        var bothEntered = CrawlSignal();
        int children = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/") await RespondAsync(context, rootBody);
            else {
                if (Interlocked.Increment(ref children) == 2) bothEntered.TrySetResult(true);
                await AwaitCrawlSignalAsync(bothEntered.Task, token);
                await RespondAsync(context, "<main>" + new string('x', 1000) + "</main>");
            }
        });
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(rootBody) + 11;
        HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(server.Url, options));
        Assert.Equal(options.MaximumTotalPageResponseBytes + 1, error.ResponseBytesRead);
        Assert.Equal(2, Volatile.Read(ref children));
    }

    [Fact]
    public async Task CrawlAsync_CancellationDrainsPrefetchedBodiesAndPreservesTheCallerToken() {
        var bothEntered = CrawlSignal();
        var release = CrawlSignal();
        int children = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>");
            else {
                if (Interlocked.Increment(ref children) == 2) bothEntered.TrySetResult(true);
                await SendHeldCrawlBodyAsync(context, "<main>Held body</main>", release.Task, token);
            }
        });
        using CancellationTokenSource cancellation = new();
        Task<HtmlCrawlResult> crawling = HtmlCrawler.CrawlAsync(server.Url, ConcurrentOptions(3, 2), cancellation.Token);
        try {
            await AwaitCrawlSignalAsync(bothEntered.Task);
            cancellation.Cancel();
            Assert.Same(crawling, await Task.WhenAny(crawling, Task.Delay(5000)));
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => crawling);
            Assert.Equal(cancellation.Token, error.CancellationToken);
        } finally { release.TrySetResult(true); }
    }

    [Theory]
    [InlineData("render")]
    [InlineData("auto-render")]
    [InlineData("auto-profile")]
    public async Task CrawlAsync_ConcurrentDownloadsRejectDynamicBrowserOwnership(string mode) {
        HtmlCrawlOptions options = ConcurrentOptions(3, 2);
        options.Render = mode == "render";
        options.AutoRender = mode == "auto-render";
        options.AutoProfile = mode == "auto-profile";
        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() => HtmlCrawler.CrawlAsync("https://example.test/", options));
        Assert.Equal(nameof(HtmlCrawlOptions.MaxConcurrentPageRequests), error.ParamName);
    }

    private static HtmlCrawlOptions ConcurrentOptions(int pages, int perOrigin) {
        HtmlCrawlOptions options = StaticOptions(pages);
        options.MaxConcurrentPageRequests = 2;
        options.MaxConcurrentPageRequestsPerOrigin = perOrigin;
        return options;
    }
}
