using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_ReleasedSkippedCacheSurvivesResumedCheckpoint(bool newOutput) {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        using CancellationTokenSource cancellation = new();
        bool interrupt = true;
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            context.Response.Headers["ETag"] = "\"" + path + "\"";
            if (path == "/next" && interrupt) cancellation.Cancel();
            await RespondAsync(context, path == "/next" ? "<main>Next body</main>"
                : "<main>Shared body</main><a href='/duplicate'></a><a href='/next'></a><script>skippedCacheSentinel</script>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(3);
            options.OutputPath = source;
            options.CacheResponses = true;
            options.RetainPageContent = false;
            options.DeduplicatePages = true;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlCrawler.CrawlAsync(root, options, cancellation.Token));
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(source);
            HtmlCrawlPage skipped = Assert.Single(checkpoint.SkippedPages);
            Assert.Equal(HtmlCrawlSkipReason.DuplicateContent, skipped.SkipReason);
            Assert.NotNull(skipped.HttpCache);
            Assert.Contains("skippedCacheSentinel", skipped.HttpCache.Html);

            interrupt = false;
            options.ResumePath = source;
            if (newOutput) options.OutputPath = Path.Combine(directory, "resumed");
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, resumed.Pages.Count);
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(options.OutputPath);
            HtmlCrawlPage savedSkipped = Assert.Single(loaded.SkippedPages);
            Assert.NotNull(savedSkipped.HttpCache);
            Assert.Contains("skippedCacheSentinel", savedSkipped.HttpCache.Html);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
