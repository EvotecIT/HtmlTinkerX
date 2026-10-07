using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task CrawlAsync_HttpRetryRecoversSelectedTransientResponses(int status) {
        int requests = 0;
        string? method = null;
        using HttpListener server = StartFlexibleServer(async context => {
            method = context.Request.HttpMethod;
            if (Interlocked.Increment(ref requests) == 1) {
                context.Response.StatusCode = status;
                context.Response.Headers["Retry-After"] = "0";
            } else {
                await RespondAsync(context, "<main>Recovered page</main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 1;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Equal(200, page.StatusCode);
        Assert.Contains("Recovered page", page.Text);
        Assert.Equal(2, Volatile.Read(ref requests));
        Assert.Equal("GET", method);
    }

    [Theory]
    [InlineData(0, 503, 1)]
    [InlineData(2, 404, 1)]
    [InlineData(2, 401, 1)]
    [InlineData(2, 503, 3)]
    public async Task CrawlAsync_HttpRetryPreservesDefaultsAndStopsAtTheLimit(int retries, int status, int expectedRequests) {
        int requests = 0;
        using HttpListener server = StartFlexibleServer(context => {
            Interlocked.Increment(ref requests);
            context.Response.StatusCode = status;
            context.Response.Headers["Retry-After"] = "0";
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = retries;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Equal(status, page.StatusCode);
        Assert.Equal(expectedRequests, Volatile.Read(ref requests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_HttpRetryHonorsSecondsAndDates(bool useDate) {
        int requests = 0;
        DateTimeOffset earliest = default;
        DateTimeOffset retried = default;
        using HttpListener server = StartFlexibleServer(async context => {
            if (Interlocked.Increment(ref requests) == 1) {
                context.Response.StatusCode = 429;
                earliest = useDate ? DateTimeOffset.UtcNow.AddSeconds(2) : DateTimeOffset.UtcNow.AddSeconds(1);
                string value = useDate ? earliest.ToString("R", CultureInfo.InvariantCulture) : "1";
                if (useDate) earliest = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
                context.Response.Headers["Retry-After"] = value;
            } else {
                retried = DateTimeOffset.UtcNow;
                await RespondAsync(context, "<main>Recovered</main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 1;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Equal(2, Volatile.Read(ref requests));
        Assert.True(retried >= earliest, $"Retried at {retried:O}, before {earliest:O}.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task CrawlAsync_HttpRetryBacksOffWithoutAValidServerDelay(string? retryAfter) {
        var arrivals = new List<long>();
        using HttpListener server = StartFlexibleServer(async context => {
            arrivals.Add(Stopwatch.GetTimestamp());
            if (arrivals.Count <= 2) {
                context.Response.StatusCode = 503;
                if (retryAfter != null) context.Response.Headers["Retry-After"] = retryAfter;
            } else {
                await RespondAsync(context, "<main>Recovered</main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 2;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Equal(3, arrivals.Count);
        double firstDelay = (arrivals[1] - arrivals[0]) / (double)Stopwatch.Frequency;
        double secondDelay = (arrivals[2] - arrivals[1]) / (double)Stopwatch.Frequency;
        Assert.True(firstDelay >= 0.25, $"First retry arrived after {firstDelay * 1000:F3} ms, before the 250 ms backoff.");
        Assert.True(secondDelay >= 0.5, $"Second retry arrived after {secondDelay * 1000:F3} ms, before the 500 ms backoff.");
    }

    [Fact]
    public async Task CrawlAsync_HttpRetryDoesNotShortenAWaitBeyondTheTimeout() {
        int requests = 0;
        using HttpListener server = StartFlexibleServer(context => {
            Interlocked.Increment(ref requests);
            context.Response.StatusCode = 503;
            context.Response.Headers["Retry-After"] = "86400";
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 2;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Equal(503, page.StatusCode);
        Assert.Equal(1, Volatile.Read(ref requests));
    }

    [Fact]
    public async Task CrawlAsync_HttpRetrySharesTheRequestDeadlineAcrossAttempts() {
        int requests = 0;
        using HttpListener server = StartFlexibleServer(context => {
            Interlocked.Increment(ref requests);
            context.Response.StatusCode = 503;
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 10;
        options.Timeout = 1200;

        Task<HtmlCrawlResult> crawl = HtmlCrawler.CrawlAsync(root, options);
        Assert.Same(crawl, await Task.WhenAny(crawl, Task.Delay(5000)));
        HtmlCrawlPage page = Assert.Single((await crawl).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.InRange(Volatile.Read(ref requests), 1, 3);
        Assert.NotEmpty(page.Error!);
    }

    [Fact]
    public async Task CrawlAsync_HttpRetryPropagatesCancellationAfterATransientResponse() {
        int requests = 0;
        var responseSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpListener server = StartFlexibleServer(context => {
            Interlocked.Increment(ref requests);
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "4";
            context.Response.Close();
            responseSent.TrySetResult(true);
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 1;
        using var cancellation = new CancellationTokenSource();

        Task<HtmlCrawlResult> crawl = HtmlCrawler.CrawlAsync(root, options, cancellation.Token);
        Assert.Same(responseSent.Task, await Task.WhenAny(responseSent.Task, Task.Delay(5000)));
        await Task.Delay(100);
        cancellation.Cancel();
        Assert.Same(crawl, await Task.WhenAny(crawl, Task.Delay(2000)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => crawl);
        Assert.Equal(1, Volatile.Read(ref requests));
    }

    [Fact]
    public async Task CrawlAsync_HttpRetryKeepsOneRetryBudgetAndCredentialsScopedAcrossRedirects() {
        int firstRequests = 0;
        int otherRequests = 0;
        var firstCredentials = new List<string?>();
        var otherCredentials = new List<string?>();
        using HttpListener other = StartFlexibleServer(async context => {
            otherCredentials.Add(context.Request.Headers["Authorization"]);
            if (Interlocked.Increment(ref otherRequests) == 1) {
                context.Response.StatusCode = 503;
                context.Response.Headers["Retry-After"] = "0";
            } else {
                await RespondAsync(context, "<main>Unexpected extra retry</main>");
            }
        }, out string otherRoot);
        using HttpListener server = StartFlexibleServer(context => {
            firstCredentials.Add(context.Request.Headers["Authorization"]);
            if (Interlocked.Increment(ref firstRequests) == 1) {
                context.Response.StatusCode = 503;
                context.Response.Headers["Retry-After"] = "0";
            } else {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = otherRoot;
            }
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 1;
        options.Headers["Authorization"] = "Bearer test-token";

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Equal(503, page.StatusCode);
        Assert.Equal(2, Volatile.Read(ref firstRequests));
        Assert.All(firstCredentials, value => Assert.Equal("Bearer test-token", value));
        Assert.Null(Assert.Single(otherCredentials));
        Assert.Equal(1, Volatile.Read(ref otherRequests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_HttpRetryRevalidatesTheSameCachedRepresentationUnlessTheSessionChanges(bool setCookie) {
        int validations = 0;
        int authenticatedDownloads = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"original\"";
            if (context.Request.Headers["If-None-Match"] == "\"original\"") {
                context.Response.StatusCode = Interlocked.Increment(ref validations) == 1 ? 503 : 304;
                context.Response.Headers["Retry-After"] = "0";
                if (setCookie) context.Response.Headers["Set-Cookie"] = "session=changed; Path=/";
            } else if (context.Request.Headers["Cookie"] != null) {
                Interlocked.Increment(ref authenticatedDownloads);
                await RespondAsync(context, "<main>Session page</main>");
            } else {
                await RespondAsync(context, "<main>Cached page</main>");
            }
        }, out string root);
        string output = Path.Combine(Path.GetTempPath(), "htmltinkerx-retry-" + Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.OutputPath = output;
            options.CacheResponses = true;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = output;
            options.HttpRetryCount = 1;

            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

            Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
            Assert.Equal(setCookie ? 200 : 304, page.StatusCode);
            Assert.Equal(!setCookie, page.ResponseRevalidated);
            Assert.Contains(setCookie ? "Session page" : "Cached page", page.Text);
            Assert.Equal(setCookie ? 1 : 2, Volatile.Read(ref validations));
            Assert.Equal(setCookie ? 1 : 0, Volatile.Read(ref authenticatedDownloads));
            if (setCookie) Assert.Null(page.HttpCache);
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task CrawlAsync_HttpRetryCoversRobotsSitemapsPagesAndAssets() {
        var attempts = new Dictionary<string, int>(StringComparer.Ordinal);
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            attempts.TryGetValue(path, out int count);
            attempts[path] = count + 1;
            if (count == 0) {
                context.Response.StatusCode = 503;
                context.Response.Headers["Retry-After"] = "0";
            } else if (path == "/robots.txt") {
                await RespondAsync(context, "User-agent: *\nAllow: /", "text/plain");
            } else if (path == "/site.xml") {
                await RespondAsync(context, $"<urlset><url><loc>{new Uri(context.Request.Url, "/unlinked")}</loc></url></urlset>", "application/xml");
            } else if (path == "/logo.svg") {
                await RespondAsync(context, "<svg xmlns='http://www.w3.org/2000/svg'/>", "image/svg+xml");
            } else {
                await RespondAsync(context, "<main>Page body<img src='/logo.svg'></main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        options.HttpRetryCount = 1;
        options.RespectRobotsTxt = true;
        options.UseSitemaps = true;
        options.SitemapUrls.Add(root + "site.xml");
        options.DownloadAssets = true;

        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);

        Assert.Equal(2, result.Pages.Count);
        Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        HtmlCrawlAsset asset = Assert.Single(result.Assets);
        Assert.Equal(200, asset.StatusCode);
        Assert.Null(asset.Error);
        Assert.True(asset.ContentLength > 0);
        foreach (string path in new[] { "/robots.txt", "/site.xml", "/", "/unlinked", "/logo.svg" }) {
            Assert.Equal(2, attempts[path]);
        }
    }

    [Fact]
    public async Task CrawlAsync_HttpRetryHonorsRetryAfterOnRedirects() {
        DateTimeOffset earliest = default;
        DateTimeOffset followed = default;
        long firstArrival = 0;
        long finalArrival = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/") {
                firstArrival = Stopwatch.GetTimestamp();
                earliest = DateTimeOffset.UtcNow.AddSeconds(1);
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/final";
                context.Response.Headers["Retry-After"] = "1";
            } else {
                finalArrival = Stopwatch.GetTimestamp();
                followed = DateTimeOffset.UtcNow;
                await RespondAsync(context, "<main>Final page</main>");
            }
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.HttpRetryCount = 1;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Contains("Final page", page.Text);
        Assert.True(followed >= earliest, $"Redirect followed at {followed:O}, before {earliest:O}; monotonic elapsed {(finalArrival - firstArrival) / (double)Stopwatch.Frequency * 1000:F3} ms.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task CrawlAsync_HttpRetryRejectsCountsOutsideTheSupportedRange(int retries) {
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HtmlCrawler.CrawlAsync(
            "https://example.test/", new HtmlCrawlOptions { HttpRetryCount = retries }));
        Assert.Equal(nameof(HtmlCrawlOptions.HttpRetryCount), error.ParamName);
    }
}
