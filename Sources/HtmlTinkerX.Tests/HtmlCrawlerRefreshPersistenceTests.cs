using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_RefreshLoadsOriginalBodiesFromAnInterruptedCheckpoint() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        TaskCompletionSource<bool> waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cancellation = new();
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/waiting") {
                waiting.TrySetResult(true);
                await release.Task;
            }
            context.Response.Headers["ETag"] = "\"root\"";
            if (context.Request.Headers["If-None-Match"] == "\"root\"") context.Response.StatusCode = 304;
            else await RespondAsync(context, "<main>Main body</main><section id='alternate'>Alternate body</section><a href='/waiting'></a>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2);
            options.CacheResponses = true;
            options.Selector = "main";
            options.OutputPath = source;
            Task<HtmlCrawlResult> crawl = HtmlCrawler.CrawlAsync(root, options, cancellation.Token);
            Assert.Same(waiting.Task, await Task.WhenAny(waiting.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await crawl);
            release.TrySetResult(true);
            options.RefreshPath = source;
            options.OutputPath = null;
            options.MaxPages = 1;
            options.Selector = "#alternate";
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.True(page.ResponseRevalidated);
            Assert.Contains("Alternate body", page.Text);
            Assert.DoesNotContain("Main body", page.Text);
        } finally {
            cancellation.Cancel();
            release.TrySetResult(true);
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshStopsRetainingABodyWhenValidationReturnsNoStore() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string refreshed = Path.Combine(directory, "refreshed");
        int downloads = 0;
        int validations = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"original\"";
            if (context.Request.Headers["If-None-Match"] != null) {
                validations++;
                context.Response.StatusCode = 304;
                context.Response.Headers["Cache-Control"] = "no-store";
            } else {
                downloads++;
                await RespondAsync(context, "<main>Original body</main><script>unselectedBodySentinel</script>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.Selector = "main";
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            options.RefreshPath = source;
            options.OutputPath = refreshed;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.True(page.ResponseRevalidated);
            Assert.Contains("Original body", page.Text);
            string manifest = File.ReadAllText(Path.Combine(refreshed, "crawl-result.json"));
            Assert.DoesNotContain("\"HttpCache\"", manifest);
            Assert.DoesNotContain("unselectedBodySentinel", manifest);
            options.RefreshPath = refreshed;
            options.OutputPath = null;
            HtmlCrawlPage again = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(again.Status == HtmlCrawlPageStatus.Success, again.Error);
            Assert.False(again.ResponseRevalidated);
            Assert.Equal(2, downloads);
            Assert.Equal(1, validations);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshRevalidatesDuplicateBodiesFromTheSavedSkippedRecords() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string refreshed = Path.Combine(directory, "refreshed");
        int validations = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            bool duplicate = context.Request.Url!.AbsolutePath == "/duplicate";
            string tag = duplicate ? "\"duplicate\"" : "\"root\"";
            context.Response.Headers["ETag"] = tag;
            if (context.Request.Headers["If-None-Match"] == tag) {
                validations++;
                context.Response.StatusCode = 304;
            } else {
                await RespondAsync(context, duplicate ? "<main>Same body</main>"
                    : "<main>Same body</main><a href='/duplicate'></a>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2);
            options.DeduplicatePages = true;
            options.CacheResponses = true;
            options.OutputPath = source;
            HtmlCrawlPage initial = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).SkippedPages);
            options.RefreshPath = source;
            options.OutputPath = refreshed;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage duplicate = Assert.Single(result.SkippedPages);
            Assert.Equal(HtmlCrawlSkipReason.DuplicateContent, duplicate.SkipReason);
            Assert.True(duplicate.ResponseRevalidated);
            Assert.False(duplicate.ResponseChanged);
            Assert.Equal(initial.ResponseContentHash, duplicate.ResponseContentHash);
            Assert.Equal(2, validations);
            HtmlCrawlPage loaded = Assert.Single((await HtmlCrawler.LoadResultAsync(refreshed)).SkippedPages);
            Assert.True(loaded.ResponseRevalidated);
            Assert.Equal(initial.ResponseContentHash, loaded.ResponseContentHash);
            using JsonDocument record = JsonDocument.Parse(File.ReadAllText(result.SkippedPagesJsonlPath!));
            Assert.True(record.RootElement.GetProperty("ResponseRevalidated").GetBoolean());
            Assert.Equal(initial.ResponseContentHash, record.RootElement.GetProperty("ResponseContentHash").GetString());
            options.RefreshPath = refreshed;
            options.OutputPath = null;
            HtmlCrawlPage again = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).SkippedPages);
            Assert.True(again.ResponseRevalidated);
            Assert.Equal(4, validations);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
