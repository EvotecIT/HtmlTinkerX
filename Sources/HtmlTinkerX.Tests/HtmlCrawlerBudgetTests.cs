using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public async Task ReadResponseBytes_ChargesCompletedReadsWhenTheRequestDeadlineWins(int completedBytes) {
        HtmlCrawlResponseBudget budget = new(nameof(HtmlCrawlOptions.MaximumTotalPageResponseBytes), 8);
        using CancellationTokenSource requestDeadline = new();
        using HttpResponseMessage first = new() {
            Content = new StreamContent(new CancelAfterReadStream(new byte[completedBytes], requestDeadline))
        };
        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlUtilities.ReadResponseBytesAsync(first, 16, requestDeadline.Token, budget));
        Assert.Equal(requestDeadline.Token, canceled.CancellationToken);
        using HttpResponseMessage second = new() { Content = new StreamContent(new MemoryStream(new byte[4])) };
        HtmlCrawlBudgetExceededException exceeded = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() =>
            HtmlUtilities.ReadResponseBytesAsync(second, 16, CancellationToken.None, budget));
        Assert.Equal(9, exceeded.ResponseBytesRead);
    }

    private sealed class CancelAfterReadStream(byte[] bytes, CancellationTokenSource requestDeadline) : MemoryStream(bytes) {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            int read = Read(buffer, offset, count);
            requestDeadline.Cancel();
            return Task.FromResult(read);
        }
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetAllowsExactBoundaryAndResetsWhenOptionsAreReused() {
        const string rootBody = "<main>First</main><a href='/child'>Next</a>";
        const string childBody = "<main>Second</main>";
        using HttpListener server = StartServer(new Dictionary<string, string> { ["/"] = rootBody, ["/child"] = childBody }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(rootBody + childBody);
        for (int invocation = 0; invocation < 2; invocation++) {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, result.PageCount);
            Assert.Equal(0, result.FailedPageCount);
            Assert.Contains("Second", result.Pages[1].Text);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_TotalPageBudgetStopsKnownLengthAndChunkedBodies(bool chunked) {
        const string body = "<main>";
        int requests = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            requests++;
            context.Response.ContentType = "text/html";
            byte[] bytes = Encoding.UTF8.GetBytes(body + new string('x', 100000));
            if (chunked) context.Response.SendChunked = true;
            else context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(3);
        options.MaximumTotalPageResponseBytes = 17;
        HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
        Assert.Equal(nameof(HtmlCrawlOptions.MaximumTotalPageResponseBytes), error.OptionName);
        Assert.Equal(17, error.LimitBytes);
        Assert.Equal(18, error.ResponseBytesRead);
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData("robots")]
    [InlineData("sitemap")]
    [InlineData("nested-sitemap")]
    public async Task CrawlAsync_TotalPageBudgetIncludesDiscoveryResponses(string discovery) {
        string robots = "User-agent: *\nAllow: /\n";
        const string sitemap = "<urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'></urlset>";
        string index = "<sitemapindex><sitemap><loc>/child.xml</loc></sitemap></sitemapindex>";
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Page</main>", ["/robots.txt"] = robots,
            ["/sitemap.xml"] = discovery == "nested-sitemap" ? index : sitemap, ["/child.xml"] = sitemap
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        if (discovery == "robots") options.RespectRobotsTxt = true;
        else options.SitemapUrls.Add(root + "sitemap.xml");
        options.MaximumTotalPageResponseBytes = discovery == "robots" ? Encoding.UTF8.GetByteCount(robots)
            : Encoding.UTF8.GetByteCount(discovery == "nested-sitemap" ? index : sitemap);
        HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
        Assert.Equal(options.MaximumTotalPageResponseBytes + 1, error.ResponseBytesRead);
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetCountsBodiesRejectedAfterReading() {
        const string rootBody = "<main>First</main><a href='/child'>Next</a>";
        using HttpListener server = StartFlexibleServer(context => context.Request.Url!.AbsolutePath == "/"
            ? RespondAsync(context, rootBody, "application/json") : RespondAsync(context, "<main>Child</main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        // The first response is read before its unsupported content type is classified.
        options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(rootBody) - 1;
        await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetIncludesBytesReadBeforeAPerResponseFailure() {
        const string rootBody = "<a href='/large'>Large</a><a href='/last'>Last</a>";
        int requests = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            requests++;
            if (context.Request.Url!.AbsolutePath == "/large") {
                context.Response.ContentType = "text/html";
                context.Response.SendChunked = true;
                byte[] bytes = Encoding.UTF8.GetBytes(new string('x', 129));
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            } else await RespondAsync(context, context.Request.Url.AbsolutePath == "/" ? rootBody : "<main>Last</main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(3);
        options.MaximumPageResponseBytes = 128;
        options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(rootBody) + 129 + 1;
        HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
        Assert.Equal(options.MaximumTotalPageResponseBytes + 1, error.ResponseBytesRead);
        Assert.Equal(3, requests);
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetDoesNotChargeUnreadRedirectBodies() {
        const string body = "<main>Final</main>";
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/") {
                context.Response.RedirectLocation = "/final";
                context.Response.StatusCode = 302;
                await RespondAsync(context, new string('x', 100000));
            } else await RespondAsync(context, body);
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(body);
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Equal(root + "final", page.Url);
    }

    [Fact]
    public async Task CrawlAsync_TotalAssetBudgetIncludesNestedCssAndPreservesPageBudget() {
        const string html = "<html><head><link rel='stylesheet' href='/site.css'></head><body>Page</body></html>";
        const string css = "@import '/nested.css';";
        using HttpListener server = StartFlexibleServer(context => context.Request.Url!.AbsolutePath switch {
            "/" => RespondAsync(context, html),
            "/site.css" => RespondAsync(context, css, "text/css"),
            _ => RespondAsync(context, "body { color: blue; }", "text/css")
        }, out string root);
        string directory = Path.Combine(Path.GetTempPath(), "HtmlCrawlerBudgetTests", Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.OutputPath = directory;
            options.DownloadAssets = true;
            options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(html);
            options.MaximumTotalAssetResponseBytes = Encoding.UTF8.GetByteCount(css);
            HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
            Assert.Equal(nameof(HtmlCrawlOptions.MaximumTotalAssetResponseBytes), error.OptionName);
            Assert.Equal(options.MaximumTotalAssetResponseBytes + 1, error.ResponseBytesRead);
            options.ResumePath = directory;
            options.MaximumTotalAssetResponseBytes = 1024;
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(1, resumed.PageCount);
            Assert.Equal(2, resumed.AssetCount);
            Assert.All(resumed.Assets, asset => Assert.Null(asset.Error));
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetLeavesCheckpointResumableWithAFreshBudget() {
        const string rootBody = "<main>First</main><a href='/child'>Next</a>";
        const string childBody = "<main>Second</main>";
        using HttpListener server = StartServer(new Dictionary<string, string> { ["/"] = rootBody, ["/child"] = childBody }, out string root);
        string directory = Path.Combine(Path.GetTempPath(), "HtmlCrawlerBudgetTests", Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(2);
            options.OutputPath = directory;
            options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(rootBody);
            await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => HtmlCrawler.CrawlAsync(root, options));
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(directory);
            Assert.Single(checkpoint.Pages);
            Assert.Equal(root + "child", Assert.Single(checkpoint.PendingPages).Url);
            options.ResumePath = directory;
            options.MaximumTotalPageResponseBytes = Encoding.UTF8.GetByteCount(childBody);
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, result.PageCount);
            Assert.Contains("Second", result.Pages[1].Text);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_TotalPageBudgetDoesNotChargeARevalidatedCachedBody() {
        string directory = Path.Combine(Path.GetTempPath(), "HtmlCrawlerBudgetTests", Guid.NewGuid().ToString("N"));
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"cached\"";
            if (context.Request.Headers["If-None-Match"] == "\"cached\"") context.Response.StatusCode = 304;
            else await RespondAsync(context, "<main>Cached page longer than one byte</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = directory;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = directory;
            options.MaximumTotalPageResponseBytes = 1;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.ResponseRevalidated);
            Assert.Contains("Cached page", page.Text);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    public async Task CrawlAsync_TotalBudgetsRejectNonPositiveLimits(bool pageBudget, long limit) {
        HtmlCrawlOptions options = StaticOptions(1);
        if (pageBudget) options.MaximumTotalPageResponseBytes = limit;
        else options.MaximumTotalAssetResponseBytes = limit;
        ArgumentOutOfRangeException error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HtmlCrawler.CrawlAsync("http://localhost/", options));
        Assert.Equal(pageBudget ? nameof(HtmlCrawlOptions.MaximumTotalPageResponseBytes) : nameof(HtmlCrawlOptions.MaximumTotalAssetResponseBytes), error.ParamName);
    }
}
