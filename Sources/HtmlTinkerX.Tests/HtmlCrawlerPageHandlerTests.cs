using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task StreamAsync_DeliversHandlerItemsWhileTheNextPageIsStillPending() {
        TaskCompletionSource<bool> releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool secondServed = false;
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/second") {
                await Task.WhenAny(releaseSecond.Task, Task.Delay(10000));
                secondServed = true;
                await RespondAsync(context, "<main>Second</main>");
            } else {
                await RespondAsync(context, "<main><h1>Home</h1><a href='/second'>Next</a></main>");
            }
        }, out string root);

        var entries = new List<HtmlCrawlStreamEntry>();
        bool itemArrivedBeforeSecondPage = false;
        await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, StaticOptions(2), (page, token) => {
            page.Emit(page.Document.QuerySelector("h1")?.TextContent ?? page.Page.Url);
            return Task.CompletedTask;
        })) {
            if (entry.Kind == HtmlCrawlStreamEntryKind.Item && entries.Count(e => e.Kind == HtmlCrawlStreamEntryKind.Item) == 0) {
                itemArrivedBeforeSecondPage = !secondServed;
                releaseSecond.TrySetResult(true);
            }
            entries.Add(entry);
        }

        Assert.True(itemArrivedBeforeSecondPage);
        Assert.Equal(new[] {
            HtmlCrawlStreamEntryKind.Page, HtmlCrawlStreamEntryKind.Item,
            HtmlCrawlStreamEntryKind.Page, HtmlCrawlStreamEntryKind.Item,
            HtmlCrawlStreamEntryKind.Completed
        }, entries.Select(entry => entry.Kind));
        Assert.Equal("Home", entries[1].Item);
        Assert.Same(entries[0].Page, entries[1].Page);
        Assert.Equal(2, entries[4].Result!.Pages.Count);
    }

    [Fact]
    public async Task StreamAsync_CommitsAPageCheckpointOnlyAfterTheConsumerFinishesIt() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpListener server = StartServer(new Dictionary<string, string> { ["/"] = "<main>Home</main>" }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.OutputPath = output;
        try {
            int committedWhileConsumerBusy = -1;
            await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, options)) {
                if (entry.Kind != HtmlCrawlStreamEntryKind.Page) continue;
                await Task.Delay(500);
                committedWhileConsumerBusy = (await HtmlCrawler.LoadResultAsync(output)).Pages.Count;
            }

            Assert.Equal(0, committedWhileConsumerBusy);
            Assert.Single((await HtmlCrawler.LoadResultAsync(output)).Pages);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task StreamAsync_HigherPriorityFollowUpsDoNotExceedThePageBudgetWhilePrefetching() {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context, "<main><a href='/a'>A</a><a href='/b'>B</a></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(3);
        options.MaxDepth = 3;
        options.MaxConcurrentPageRequests = 2;

        HtmlCrawlResult? result = null;
        await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, options, (page, token) => {
            string path = new Uri(page.Page.Url).AbsolutePath;
            if (path == "/") page.Follow("/high", priority: 5);
            if (path == "/high") page.Follow("/higher", priority: 10);
            return Task.CompletedTask;
        })) {
            result = entry.Result ?? result;
        }

        Assert.Equal(3, result!.Pages.Count);
    }

    [Fact]
    public async Task StreamAsync_FollowUpsResolveAgainstTheDocumentBase() {
        var requested = new ConcurrentQueue<string>();
        using HttpListener server = StartFlexibleServer(context => {
            requested.Enqueue(context.Request.Url!.AbsolutePath);
            return RespondAsync(context, "<head><base href='/docs/'></head><main>Page</main>");
        }, out string root);

        await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, StaticOptions(2), (page, token) => {
            page.Follow("next");
            return Task.CompletedTask;
        })) { }

        Assert.Equal(new[] { "/", "/docs/next" }, requested);
    }

    [Fact]
    public async Task StreamAsync_FollowUpsObeyScopeRobotsDedupeAndPageBudget() {
        var requested = new ConcurrentQueue<string>();
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            requested.Enqueue(path);
            if (path == "/robots.txt") await RespondAsync(context, "User-agent: *\nDisallow: /blocked", "text/plain");
            else await RespondAsync(context, $"<main>{path}<a href='/linked'>Linked</a></main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        options.RespectRobotsTxt = true;
        string offHost = root.Replace("localhost", "127.0.0.1") + "outside";
        var handlerData = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        HtmlCrawlResult? result = null;
        await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, options, (page, token) => {
            handlerData[new Uri(page.Page.Url).AbsolutePath] = page.Data;
            page.FollowLinks = false;
            page.Follow("a", new Hashtable { ["kind"] = "a" });
            page.Follow("/a");
            page.Follow("/b", new Hashtable { ["kind"] = "b", ["rank"] = 2 }, priority: 5);
            page.Follow("/blocked", priority: 10);
            page.Follow(offHost, priority: 10);
            return Task.CompletedTask;
        })) {
            result = entry.Result ?? result;
        }

        string[] pages = requested.Where(path => path != "/robots.txt").ToArray();
        Assert.Equal(new[] { "/", "/b" }, pages);
        Assert.Equal("b", handlerData["/b"]["kind"]);
        Assert.Equal("2", handlerData["/b"]["rank"]);
        Assert.Contains(result!.SkippedPages, page => page.Url.EndsWith("/blocked") && page.SkipReason == HtmlCrawlSkipReason.DisallowedByRobots);
        Assert.Contains(result.SkippedPages, page => page.Url == offHost && page.SkipReason == HtmlCrawlSkipReason.OutsideHost);
        HtmlCrawlPendingItem pending = Assert.Single(result.PendingPages);
        Assert.EndsWith("/a", pending.Url);
        Assert.Equal("a", pending.Data!["kind"]);
    }

    [Fact]
    public async Task StreamAsync_RenderedFollowUpLoadsOnlyThatPageInTheBrowser() {
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Static home</main>",
            ["/app"] = "<main id='app'>Loading</main><script>document.getElementById('app').textContent = 'Script content';</script>"
        }, out string root);
        var pages = new List<HtmlCrawlPage>();
        await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, StaticOptions(2), (page, token) => {
            page.Follow("/app", render: true);
            return Task.CompletedTask;
        })) {
            if (entry.Kind == HtmlCrawlStreamEntryKind.Page) pages.Add(entry.Page!);
        }

        Assert.Equal(2, pages.Count);
        Assert.False(pages[0].Rendered);
        Assert.True(pages[1].Rendered);
        Assert.Contains("Script content", pages[1].Text);
    }

    [Fact]
    public async Task StreamAsync_HandlerFailureSurfacesAndKeepsTheLastCommittedCheckpoint() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Home</main>", ["/next"] = "<main>Second page</main>"
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        options.OutputPath = output;
        var entries = new List<HtmlCrawlStreamEntry>();
        try {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await foreach (HtmlCrawlStreamEntry entry in HtmlCrawler.StreamAsync(root, options, (page, token) => {
                    if (page.Depth == 1) throw new InvalidOperationException("handler failed");
                    page.Follow("/next", new Hashtable { ["section"] = "news" }, priority: 3);
                    return Task.CompletedTask;
                })) {
                    entries.Add(entry);
                }
            });

            Assert.Equal("handler failed", failure.Message);
            Assert.Equal(HtmlCrawlStreamEntryKind.Page, Assert.Single(entries).Kind);
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(output);
            Assert.Equal(root, Assert.Single(checkpoint.Pages).Url);
            HtmlCrawlPendingItem pending = Assert.Single(checkpoint.PendingPages);
            Assert.EndsWith("/next", pending.Url);
            Assert.Equal("news", pending.Data!["section"]);
            Assert.Equal(3, pending.Priority);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }
}
