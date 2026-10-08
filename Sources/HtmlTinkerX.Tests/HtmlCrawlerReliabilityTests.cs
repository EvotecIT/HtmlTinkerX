using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    public async Task CrawlAsync_UsesRedirectDestinationAndPreservesUrlCase() {
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/redirect") {
                context.Response.Redirect("/actual/page");
            } else {
                await RespondAsync(context, context.Request.Url.AbsolutePath == "/actual/page"
                    ? "<main><p>Root page</p><a href='Guide?id=A'>Upper</a><a href='guide?id=a'>Lower</a></main>"
                    : "<main><p>" + context.Request.RawUrl + "</p></main>");
            }
        }, out string root);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "redirect", StaticOptions(3));
        Assert.Equal(root + "redirect", result.Pages[0].RequestedUrl);
        Assert.Equal(root + "actual/page", result.Pages[0].Url);
        Assert.Contains(result.Pages, page => page.Url == root + "actual/Guide?id=A");
        Assert.Contains(result.Pages, page => page.Url == root + "actual/guide?id=a");
    }

    [Fact]
    public async Task CrawlAsync_LimitsFetchedDuplicatesAndDelaysEveryFetch() {
        ConcurrentQueue<long> times = new();
        using HttpListener server = StartFlexibleServer(async context => {
            times.Enqueue(Stopwatch.GetTimestamp());
            await RespondAsync(context, context.Request.Url!.AbsolutePath == "/"
                ? "<main><p>Root</p>" + string.Concat(Enumerable.Range(0, 6).Select(index => $"<a href='/duplicate/{index}'>next</a>")) + "</main>"
                : "<main><p>Duplicate body</p></main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(3);
        options.DelayMs = 75;
        options.DeduplicatePages = true;
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Equal(3, times.Count);
        Assert.Single(result.SkippedPages, page => page.SkipReason == HtmlCrawlSkipReason.DuplicateContent);
        long[] fetchedAt = times.ToArray();
        Assert.True((fetchedAt[2] - fetchedAt[1]) * 1000d / Stopwatch.Frequency >= 65);
        Assert.Equal(4, result.PendingPages.Count);
    }

    [Fact]
    public async Task CrawlAsync_RobotsMergesGroupsAndMatchesWildcardsAnchorsAndCase() {
        ConcurrentQueue<string> requests = new();
        string[] links = { "/private/secret", "/blocked", "/Public", "/public", "/file.pdf", "/file.pdf?download=1", "/encoded/path" };
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.RawUrl!;
            requests.Enqueue(path);
            await RespondAsync(context, path == "/robots.txt"
                ? "User-agent: HtmlTinkerX\nDisallow: /private/*\nDisallow: /public\nDisallow: /*.pdf$\nUser-agent: HtmlTinkerX\nDisallow: /blocked\nDisallow: /%65ncoded/path\nUser-agent: *\nDisallow: /Public\n"
                : path == "/" ? "<main>" + string.Concat(links.Select(link => $"<a href='{link}'>next</a>")) + "</main>" : "<main>content</main>");
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(20);
        options.RespectRobotsTxt = true;
        options.RobotsUserAgent = "HtmlTinkerX";
        options.SkipKnownAssetUrls = false;
        await HtmlCrawler.CrawlAsync(root, options);
        foreach (string denied in new[] { "/private/secret", "/blocked", "/public", "/file.pdf", "/encoded/path" }) Assert.DoesNotContain(denied, requests);
        Assert.Contains("/Public", requests);
        Assert.Contains("/file.pdf?download=1", requests);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CrawlAsync_ScopesCredentialsSeparatelyFromHostAndAssetResolution(bool restrict, bool differentHost) {
        ConcurrentQueue<(string? Auth, string? Secret)> external = new();
        using HttpListener other = StartFlexibleServer(async context => {
            external.Enqueue((context.Request.Headers["Authorization"], context.Request.Headers["X-Crawl-Secret"]));
            await RespondAsync(context, "asset", "image/png");
        }, out string otherRoot, differentHost ? "localhost" : "127.0.0.1");
        string root = string.Empty;
        ConcurrentQueue<(string? Auth, string? Secret)> trusted = new();
        using HttpListener server = StartFlexibleServer(async context => {
            trusted.Enqueue((context.Request.Headers["Authorization"], context.Request.Headers["X-Crawl-Secret"]));
            await RespondAsync(context, context.Request.Url!.AbsolutePath == "/style.css"
                ? $"body {{ background: url('{otherRoot}nested.png'); }}"
                : $"<base href='{otherRoot}'><main>content<img src='image.png'><link rel='stylesheet' href='{root}style.css'></main>",
                context.Request.Url.AbsolutePath == "/style.css" ? "text/css" : "text/html");
        }, out root, "127.0.0.1");
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.RestrictToHost = restrict; options.DownloadAssets = true; options.OutputPath = output;
            options.Username = "test-user"; options.Password = "test-password"; options.Headers["X-Crawl-Secret"] = "test-secret";
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.All(trusted, request => { Assert.NotNull(request.Auth); Assert.Equal("test-secret", request.Secret); });
            if (restrict && differentHost) {
                Assert.Empty(external);
                Assert.DoesNotContain(result.Assets, asset => asset.Url.StartsWith(otherRoot, StringComparison.Ordinal));
            } else {
                Assert.Equal(2, external.Count);
                Assert.All(external, request => { Assert.Null(request.Auth); Assert.Null(request.Secret); });
            }
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_CrossOriginRedirectCannotForwardHeaders() {
        ConcurrentQueue<string?> secrets = new();
        using HttpListener other = StartFlexibleServer(async context => {
            secrets.Enqueue(context.Request.Headers["X-Crawl-Secret"]);
            await RespondAsync(context, "<main>final</main>");
        }, out string otherRoot);
        using HttpListener server = StartFlexibleServer(context => {
            Assert.Equal("test-secret", context.Request.Headers["X-Crawl-Secret"]);
            context.Response.Redirect(otherRoot);
            return Task.CompletedTask;
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Headers["X-Crawl-Secret"] = "test-secret";
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Equal(otherRoot, Assert.Single(result.Pages).Url);
        Assert.Single(secrets);
        Assert.Null(secrets.Single());
    }

    [Fact]
    public async Task CrawlAsync_CancellationKeepsResumableCheckpointAndFinalizesLegacyExport() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using CancellationTokenSource cancellation = new();
        bool cancel = true;
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/next" && cancel) cancellation.Cancel();
            await RespondAsync(context, context.Request.Url.AbsolutePath == "/"
                ? "<main><p>Root</p><a href='/next'>next</a></main>" : "<main><p>Next</p></main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.OutputPath = output;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlCrawler.CrawlAsync(root, options, cancellation.Token));
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(output);
            Assert.Single(checkpoint.Pages);
            Assert.Single(checkpoint.PendingPages);
            Assert.Contains("Root", checkpoint.Pages[0].Html);
            cancel = false;
            options.ResumePath = output;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, result.Pages.Count);
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(output);
            Assert.Equal(2, loaded.Pages.Count);
            Assert.DoesNotContain("CheckpointVersion", File.ReadAllText(result.ManifestPath!));
            Assert.False(Directory.Exists(result.ManifestPath + ".state"));
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task SaveResultAsync_PreCanceledWritePreservesExistingManifest() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult original = new() { StartUrl = "https://example.org/original" };
            await HtmlCrawler.SaveResultAsync(original, output);
            string before = File.ReadAllText(original.ManifestPath!);
            using CancellationTokenSource cancellation = new(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlCrawler.SaveResultAsync(
                new HtmlCrawlResult { StartUrl = "https://example.org/replacement" }, output, cancellation.Token));
            Assert.Equal(before, File.ReadAllText(original.ManifestPath!));
            Assert.Empty(Directory.GetFiles(output, "*.tmp", SearchOption.AllDirectories));
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    private static HtmlCrawlOptions StaticOptions(int maximum) => new() {
        MaxPages = maximum, MaxDepth = 1, RespectRobotsTxt = false, UseSitemaps = false,
        AutoProfile = false, IncludeHtml = true, IncludeMarkdown = false
    };

    private static HttpListener StartFlexibleServer(Func<HttpListenerContext, Task> respond, out string root, string host = "localhost") {
        HttpListener listener = new();
        StartListenerWithFreePort(listener, out root, host);
        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = RespondAndCloseAsync(context, respond);
                }
            } catch (HttpListenerException) { } catch (ObjectDisposedException) { } catch (IOException) { }
        });
        return listener;
    }

    private static async Task RespondAndCloseAsync(HttpListenerContext context, Func<HttpListenerContext, Task> respond) {
        try { await respond(context); }
        catch (IOException) { }
        catch (HttpListenerException) { }
        finally { context.Response.Close(); }
    }

    private static async Task RespondAsync(HttpListenerContext context, string body, string contentType = "text/html") {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = contentType + "; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }
}
