using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData("\"version-two\"")]
    [InlineData("W/\"version-two\"")]
    public async Task CrawlAsync_PreservesFinalResponseValidatorsAcrossCanonicalRewriteAndExport(string tag) {
        string root = string.Empty;
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        DateTimeOffset modified = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/source") {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/document?utm_source=one&version=2#section";
                context.Response.Headers["ETag"] = "\"redirect\"";
                return;
            }
            context.Response.Headers["ETag"] = tag;
            context.Response.Headers["Last-Modified"] = modified.ToString("R", CultureInfo.InvariantCulture);
            await RespondAsync(context, "<base href='https://assets.invalid/resources/'><link rel='canonical' href='"
                + root + "canonical'><main>Document body</main>");
        }, out root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.UseCanonicalUrls = true;
            options.OutputPath = output;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "source", options);
            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.Equal(root + "canonical", page.Url);
            Assert.Equal(root + "source", page.RequestedUrl);
            Assert.Equal(root + "document?utm_source=one&version=2", page.ResponseUrl);
            Assert.Equal(tag, page.EntityTag);
            Assert.Equal(modified, page.LastModified);

            HtmlCrawlPage loaded = Assert.Single((await HtmlCrawler.LoadResultAsync(output)).Pages);
            Assert.Equal(page.ResponseUrl, loaded.ResponseUrl);
            Assert.Equal(tag, loaded.EntityTag);
            Assert.Equal(modified, loaded.LastModified);
            using JsonDocument record = JsonDocument.Parse(File.ReadAllText(result.PagesJsonlPath!));
            Assert.Equal(page.ResponseUrl, record.RootElement.GetProperty("ResponseUrl").GetString());
            Assert.Equal(tag, record.RootElement.GetProperty("EntityTag").GetString());
            Assert.Equal(modified, record.RootElement.GetProperty("LastModified").GetDateTimeOffset());
            Assert.EndsWith(",ResponseUrl,EntityTag,LastModified", File.ReadAllLines(result.PagesCsvPath!)[0]);
            Assert.Contains("\"" + tag.Replace("\"", "\"\"") + "\"", File.ReadAllLines(result.PagesCsvPath!)[1]);
            using JsonDocument sidecar = JsonDocument.Parse(File.ReadAllText(page.ManifestPath!));
            Assert.Equal(page.ResponseUrl, sidecar.RootElement.GetProperty("ResponseUrl").GetString());
            Assert.Equal(tag, sidecar.RootElement.GetProperty("EntityTag").GetString());
            Assert.Equal(modified, sidecar.RootElement.GetProperty("LastModified").GetDateTimeOffset());
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("invalid", "not a date")]
    [InlineData("*", "not a date")]
    [InlineData(null, "2026-10-03T12:00:00Z")]
    public async Task CrawlAsync_UnusableValidatorsDoNotFailThePage(string? tag, string? date) {
        using HttpListener server = StartFlexibleServer(async context => {
            if (tag != null) context.Response.Headers["ETag"] = tag;
            if (date != null) context.Response.Headers["Last-Modified"] = date;
            await RespondAsync(context, "<main>Document body</main>");
        }, out string root);
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, StaticOptions(1))).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(root, page.ResponseUrl);
        Assert.Null(page.EntityTag);
        Assert.Null(page.LastModified);
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedResponseValidatorsFollowNavigationAfterAnInteraction(HtmlBrowserEngine browser) {
        DateTimeOffset modified = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        using HttpListener server = StartFlexibleServer(async context => {
            bool final = context.Request.Url!.AbsolutePath == "/document";
            context.Response.Headers["ETag"] = final ? "W/\"final\"" : "\"initial\"";
            context.Response.Headers["Last-Modified"] = modified.AddDays(final ? 0 : -1).ToString("R", CultureInfo.InvariantCulture);
            await RespondAsync(context, final ? "<main>Final document</main>"
                : "<main>Initial document</main><button id='continue' onclick=\"location.href='/document?version=2#section'\">Continue</button>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.ClickSelectors.Add("#continue");

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(root + "document?version=2", page.ResponseUrl);
        Assert.Equal("W/\"final\"", page.EntityTag);
        Assert.Equal(modified, page.LastModified);
        Assert.Contains("Final document", page.Text);
    }

    [Fact]
    public async Task CrawlAsync_SkippedContentRetainsResponseValidatorsInItsExport() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"pdf-version\"";
            await RespondAsync(context, "PDF data", "application/pdf");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.OutputPath = output;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage page = Assert.Single(result.SkippedPages);
            Assert.Equal(HtmlCrawlSkipReason.UnsupportedContentType, page.SkipReason);
            Assert.Equal(root, page.ResponseUrl);
            Assert.Equal("\"pdf-version\"", page.EntityTag);
            using JsonDocument record = JsonDocument.Parse(File.ReadAllText(result.SkippedPagesJsonlPath!));
            Assert.Equal(root, record.RootElement.GetProperty("ResponseUrl").GetString());
            Assert.Equal(page.EntityTag, record.RootElement.GetProperty("EntityTag").GetString());
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_DuplicateContentRetainsItsOwnResponseValidators() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        DateTimeOffset modified = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        using HttpListener server = StartFlexibleServer(async context => {
            bool duplicate = context.Request.Url!.AbsolutePath == "/duplicate";
            context.Response.Headers["ETag"] = duplicate ? "\"duplicate\"" : "\"initial\"";
            context.Response.Headers["Last-Modified"] = modified.ToString("R", CultureInfo.InvariantCulture);
            await RespondAsync(context, duplicate ? "<main>Same document</main>"
                : "<main>Same document</main><a href='/duplicate'></a>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2);
            options.DeduplicatePages = true;
            options.OutputPath = output;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage page = Assert.Single(result.SkippedPages);
            Assert.Equal(HtmlCrawlSkipReason.DuplicateContent, page.SkipReason);
            Assert.Equal(root + "duplicate", page.ResponseUrl);
            Assert.Equal("\"duplicate\"", page.EntityTag);
            Assert.Equal(modified, page.LastModified);
            HtmlCrawlPage loaded = Assert.Single((await HtmlCrawler.LoadResultAsync(output)).SkippedPages);
            Assert.Equal(page.ResponseUrl, loaded.ResponseUrl);
            Assert.Equal(page.EntityTag, loaded.EntityTag);
            Assert.Equal(modified, loaded.LastModified);
            using JsonDocument record = JsonDocument.Parse(File.ReadAllText(result.SkippedPagesJsonlPath!));
            Assert.Equal(page.ResponseUrl, record.RootElement.GetProperty("ResponseUrl").GetString());
            Assert.Equal(page.EntityTag, record.RootElement.GetProperty("EntityTag").GetString());
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium, 204)]
    [InlineData(HtmlBrowserEngine.Firefox, 204)]
    [InlineData(HtmlBrowserEngine.WebKit, 204)]
    [InlineData(HtmlBrowserEngine.Chromium, 205)]
    [InlineData(HtmlBrowserEngine.Chromium, 200)]
    public async Task CrawlAsync_RenderedNonCommittingResponseKeepsTheActiveDocumentMetadata(HtmlBrowserEngine browser, int status) {
        using HttpListener server = StartFlexibleServer(async context => {
            bool other = context.Request.Url!.AbsolutePath == "/other";
            context.Response.Headers["ETag"] = other ? "\"other\"" : "\"initial\"";
            if (other) {
                context.Response.StatusCode = status;
                if (status == 200) {
                    context.Response.Headers["Content-Disposition"] = "attachment; filename=document.txt";
                    await RespondAsync(context, "Download body", "application/octet-stream");
                }
            } else {
                await RespondAsync(context, "<main>Initial document</main><a id='continue' href='/other'>Continue</a>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.ClickSelectors.Add("#continue");
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Contains("Initial document", page.Text);
        Assert.Equal(root, page.ResponseUrl);
        Assert.Equal("\"initial\"", page.EntityTag);
        Assert.Equal(200, page.StatusCode);
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedHistoryNavigationPreservesTheHttpDocumentIdentity(HtmlBrowserEngine browser) {
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"initial\"";
            await RespondAsync(context, "<main>Initial document</main><button id='continue' onclick=\"history.pushState({}, '', '/client-route')\">Continue</button>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.ClickSelectors.Add("#continue");
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(root + "client-route", page.Url);
        Assert.Equal(root, page.ResponseUrl);
        Assert.Equal("\"initial\"", page.EntityTag);
    }

    [Fact]
    public async Task CrawlAsync_InterruptedCheckpointPreservesDuplicateResponseMetadata() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        TaskCompletionSource<bool> waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cancellation = new();
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/waiting") {
                waiting.TrySetResult(true);
                await release.Task;
            }
            context.Response.Headers["ETag"] = path == "/duplicate" ? "\"duplicate\"" : "\"initial\"";
            await RespondAsync(context, path == "/" ? "<main>Same body</main><a href='/duplicate'></a><a href='/waiting'></a>"
                : "<main>Same body</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(3);
            options.OutputPath = output;
            options.DeduplicatePages = true;
            Task<HtmlCrawlResult> crawl = HtmlCrawler.CrawlAsync(root, options, cancellation.Token);
            Assert.Same(waiting.Task, await Task.WhenAny(waiting.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await crawl);
            HtmlCrawlPage duplicate = Assert.Single((await HtmlCrawler.LoadResultAsync(output)).SkippedPages);
            Assert.Equal(root + "duplicate", duplicate.ResponseUrl);
            Assert.Equal("\"duplicate\"", duplicate.EntityTag);
        } finally {
            cancellation.Cancel();
            release.TrySetResult(true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedRedirectToNoContentCannotBecomeAHistoryDocument(HtmlBrowserEngine browser) {
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            context.Response.Headers["ETag"] = path == "/" ? "\"initial\"" : "\"other\"";
            if (path == "/redirect") {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/no-content";
            } else if (path == "/no-content") {
                context.Response.StatusCode = 204;
            } else {
                await RespondAsync(context, "<main>Initial document</main><a id='continue' href='/redirect'>Continue</a>"
                    + "<button id='history' onclick=\"history.pushState({}, '', '/redirect')\">History</button>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        options.ClickSelectors.Add("#continue");
        options.ClickSelectors.Add("#history");
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(root + "redirect", page.Url);
        Assert.Equal(root, page.ResponseUrl);
        Assert.Equal("\"initial\"", page.EntityTag);
        Assert.Equal(200, page.StatusCode);
        Assert.Contains("Initial document", page.Text);
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task CrawlAsync_RenderedInitialRedirectRetainsTheFinalDocumentResponseValidators(HtmlBrowserEngine browser) {
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/") {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/document?version=two#section";
                context.Response.Headers["ETag"] = "\"redirect\"";
            } else {
                context.Response.Headers["ETag"] = "\"final\"";
                await RespondAsync(context, "<main>Final document</main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = true;
        options.Browser = browser;
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(root + "document?version=two", page.ResponseUrl);
        Assert.Equal("\"final\"", page.EntityTag);
        Assert.Equal(200, page.StatusCode);
    }
}
