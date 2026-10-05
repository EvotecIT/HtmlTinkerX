using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrawlAsync_RetainedContentOptionPreservesExportsAndSelfContainedManifest(bool retain) {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string copy = Path.Combine(directory, "copy");
        using HttpListener server = StartFlexibleServer(async context => {
            bool first = context.Request.Url!.AbsolutePath == "/";
            await RespondAsync(context, $"<main><h1>{(first ? "First" : "Second")}</h1><p>{string.Join(" ", Enumerable.Range(0, 350).Select(i => (first ? "first" : "second") + i))}</p>"
                + (first ? "<a href='/next'>Next</a>" : "") + "</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.OutputPath = source;
            options.IncludeMarkdown = true; options.RetainPageContent = retain;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, result.Pages.Count);
            Assert.All(result.Pages, page => {
                Assert.Equal(retain, page.Html.Length > 0); Assert.Equal(retain, page.Text.Length > 0);
                Assert.Equal(retain, page.Markdown.Length > 0);
                Assert.True(File.Exists(page.HtmlPath)); Assert.True(File.Exists(page.TextPath)); Assert.True(File.Exists(page.MarkdownPath));
            });
            Assert.False(Directory.Exists(result.ManifestPath + ".state"));
            Assert.Equal(retain ? 0 : 2, Directory.GetFiles(result.PagesDirectoryPath!, "*.content.json").Length);
            Assert.Contains(Path.GetFileName(result.Pages[1].HtmlPath!), File.ReadAllText(result.Pages[0].HtmlPath!));
            string[] chunks = File.ReadAllLines(result.ChunksJsonlPath!);
            Assert.Equal(6, chunks.Length); Assert.Equal(6, result.Summary.ChunkCount);
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(source);
            Assert.All(loaded.Pages, page => { Assert.NotEmpty(page.Html); Assert.NotEmpty(page.Text); Assert.NotEmpty(page.Markdown); });
            Assert.Contains("first0", loaded.Pages[0].Text); Assert.Contains("second349", loaded.Pages[1].Text);
            await HtmlCrawler.SaveResultAsync(result, copy);
            Directory.Delete(source, true); // Returned metadata now depends on the successful new export.
            HtmlCrawlResult copied = await HtmlCrawler.LoadResultAsync(copy);
            Assert.Equal(loaded.Pages.Select(p => p.Html), copied.Pages.Select(p => p.Html));
            Assert.Equal(loaded.Pages.Select(p => p.Text), copied.Pages.Select(p => p.Text));
            Assert.Equal(loaded.Pages.Select(p => p.Markdown), copied.Pages.Select(p => p.Markdown));
            Assert.Equal(chunks.Select(ChunkText), File.ReadAllLines(result.ChunksJsonlPath!).Select(ChunkText));
            await HtmlCrawler.SaveResultAsync(result, copy); // Original checkpoint and output have both gone.
            Assert.All(result.Pages, page => Assert.Equal(retain, page.Text.Length > 0));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CrawlAsync_ReleasingContentPreservesInterruptedCheckpointResume() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using CancellationTokenSource cancellation = new();
        bool interrupt = true;
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/next" && interrupt) cancellation.Cancel();
            await RespondAsync(context, context.Request.Url!.AbsolutePath == "/" ? "<main>Root<a href='/next'>Next</a></main>" : "<main>Next body</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.OutputPath = output; options.RetainPageContent = false;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlCrawler.CrawlAsync(root, options, cancellation.Token));
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(output);
            Assert.Contains("Root", Assert.Single(checkpoint.Pages).Text); Assert.Single(checkpoint.PendingPages);
            interrupt = false; options.ResumePath = output;
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, resumed.Pages.Count); Assert.All(resumed.Pages, page => Assert.Empty(page.Html));
            Assert.False(Directory.Exists(resumed.ManifestPath + ".state"));
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(output);
            Assert.Contains("Root", loaded.Pages[0].Text); Assert.Contains("Next body", loaded.Pages[1].Text);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_ReleasingContentPreservesCachedBodiesForConditionalRefresh() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        int validations = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"body\"";
            if (context.Request.Headers["If-None-Match"] != null) { validations++; context.Response.StatusCode = 304; }
            else await RespondAsync(context, "<main>Cached body</main><script>outsideSelection</script>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1); options.OutputPath = Path.Combine(directory, "source");
            options.CacheResponses = true; options.Selector = "main"; options.RetainPageContent = false;
            HtmlCrawlResult first = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Empty(Assert.Single(first.Pages).Html);
            Assert.Contains("outsideSelection", File.ReadAllText(first.ManifestPath!));
            options.RefreshPath = options.OutputPath; options.OutputPath = Path.Combine(directory, "refreshed");
            HtmlCrawlResult refreshed = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage page = Assert.Single(refreshed.Pages);
            Assert.True(page.ResponseRevalidated); Assert.Empty(page.Html); Assert.Equal(1, validations);
            Assert.Contains("Cached body", Assert.Single((await HtmlCrawler.LoadResultAsync(options.OutputPath)).Pages).Text);
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CrawlAsync_ReleasingContentRequiresPersistenceBeforeNetworkAccess() {
        HtmlCrawlOptions options = StaticOptions(1); options.RetainPageContent = false;
        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() => HtmlCrawler.CrawlAsync("http://127.0.0.1:1/", options));
        Assert.Equal(nameof(HtmlCrawlOptions.RetainPageContent), error.ParamName);
    }

    [Fact]
    public async Task SaveResultAsync_ReleasedContentSurvivesFailedExportWithoutChangingItsSource() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string copy = Path.Combine(directory, "copy");
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context, "<main>Original body</main>"), out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1); options.OutputPath = source; options.RetainPageContent = false;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage page = Assert.Single(result.Pages);
            string blockedPath = Path.Combine(copy, "pages", Path.GetFileName(page.TextPath!));
            Directory.CreateDirectory(blockedPath);
            await Assert.ThrowsAnyAsync<IOException>(() => HtmlCrawler.SaveResultAsync(result, copy));
            Assert.Empty(page.Html); Assert.Empty(page.Text); Assert.Empty(page.Markdown);
            Assert.Contains("Original body", Assert.Single((await HtmlCrawler.LoadResultAsync(source)).Pages).Text);
            Directory.Delete(blockedPath);
            await HtmlCrawler.SaveResultAsync(result, copy);
            Assert.Contains("Original body", Assert.Single((await HtmlCrawler.LoadResultAsync(copy)).Pages).Text);
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CrawlAsync_ReleasingContentPreservesDuplicateCachedBodies() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        int validations = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"duplicate\"";
            if (context.Request.Headers["If-None-Match"] != null) { validations++; context.Response.StatusCode = 304; }
            else await RespondAsync(context, "<main>Same body</main><a href='/duplicate'></a><script>cachedBodySentinel</script>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.OutputPath = Path.Combine(directory, "source");
            options.CacheResponses = true; options.RetainPageContent = false; options.DeduplicatePages = true;
            HtmlCrawlResult first = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Single(first.Pages);
            Assert.Equal(HtmlCrawlSkipReason.DuplicateContent, Assert.Single(first.SkippedPages).SkipReason);
            await HtmlCrawler.SaveResultAsync(first, Path.Combine(directory, "copy"));
            Directory.Delete(options.OutputPath, true);
            options.RefreshPath = Path.Combine(directory, "copy"); options.OutputPath = Path.Combine(directory, "refreshed");
            HtmlCrawlResult refreshed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Single(refreshed.Pages); Assert.Single(refreshed.SkippedPages);
            Assert.Equal(2, validations);
            Assert.Contains("cachedBodySentinel", File.ReadAllText(refreshed.ManifestPath!));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static string? ChunkText(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("Text").GetString();
    }
}
