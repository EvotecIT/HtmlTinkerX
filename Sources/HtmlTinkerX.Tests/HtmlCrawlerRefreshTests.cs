using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_RefreshRevalidatesTheOriginalBodyAndAppliesCurrentExtractionOptions() {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string refreshed = Path.Combine(directory, "refreshed");
        int downloads = 0;
        int validations = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "W/\"version-one\"";
            if (context.Request.Headers["If-None-Match"] == "W/\"version-one\"") {
                validations++;
                context.Response.StatusCode = 304;
            } else {
                downloads++;
                await RespondAsync(context, "<main>Main body</main><section id='alternate'>Alternate body</section>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions original = StaticOptions(1);
            original.CacheResponses = true;
            original.Selector = "main";
            original.IncludeHtml = false;
            original.OutputPath = source;
            HtmlCrawlPage initial = Assert.Single((await HtmlCrawler.CrawlAsync(root, original)).Pages);
            string manifest = File.ReadAllText(Path.Combine(source, "crawl-result.json"));
            Assert.Contains("Main body", initial.Text);
            Assert.Empty(initial.Html);
            HtmlCrawlOptions options = StaticOptions(1);
            options.RefreshPath = source;
            options.OutputPath = refreshed;
            options.Selector = "#alternate";
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.True(page.ResponseRevalidated);
            Assert.False(page.ResponseChanged);
            Assert.Equal(304, page.StatusCode);
            Assert.Equal(initial.ResponseContentHash, page.ResponseContentHash);
            Assert.Contains("Alternate body", page.Text);
            Assert.DoesNotContain("Main body", page.Text);
            Assert.Equal(manifest, File.ReadAllText(Path.Combine(source, "crawl-result.json")));
            options.RefreshPath = refreshed;
            options.OutputPath = null;
            options.Selector = "main";
            HtmlCrawlPage again = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(again.ResponseRevalidated);
            Assert.Contains("Main body", again.Text);
            Assert.Equal(1, downloads);
            Assert.Equal(2, validations);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshUsesLastModifiedWhenNoEntityTagIsAvailable() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        DateTimeOffset modified = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        using HttpListener server = StartFlexibleServer(async context => {
            string date = modified.ToString("R", CultureInfo.InvariantCulture);
            context.Response.Headers["Last-Modified"] = date;
            if (context.Request.Headers["If-Modified-Since"] == date) context.Response.StatusCode = 304;
            else await RespondAsync(context, "<main>Original body</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = source;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.True(page.ResponseRevalidated);
            Assert.Equal(modified, page.LastModified);
            Assert.Contains("Original body", page.Text);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshTraversesLinksFromARevalidatedRoot() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpListener server = StartFlexibleServer(async context => {
            bool child = context.Request.Url!.AbsolutePath == "/child";
            context.Response.Headers["ETag"] = child ? "\"child\"" : "\"root\"";
            if (!child && context.Request.Headers["If-None-Match"] == "\"root\"") context.Response.StatusCode = 304;
            else await RespondAsync(context, child ? "<main>Child body</main>" : "<main>Root body</main><a href='/child'>Child</a>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = source;
            options.MaxPages = 2;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, result.Pages.Count);
            Assert.True(result.Pages[0].ResponseRevalidated);
            Assert.False(result.Pages[1].ResponseRevalidated);
            Assert.Contains("Child body", result.Pages[1].Text);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshDoesNotReuseAResponseAcrossRequestVariants() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool conditional = false;
        using HttpListener server = StartFlexibleServer(async context => {
            conditional |= context.Request.Headers["If-None-Match"] != null;
            context.Response.Headers["ETag"] = "\"shared-tag\"";
            context.Response.Headers["Vary"] = "Accept-Language";
            await RespondAsync(context, "<main>" + context.Request.Headers["Accept-Language"] + "</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            options.Headers["Accept-Language"] = "en";
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = source;
            options.Headers["Accept-Language"] = "pl";
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.False(conditional);
            Assert.False(page.ResponseRevalidated);
            Assert.True(page.ResponseChanged);
            Assert.Contains("pl", page.Text);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Theory]
    [InlineData("no-store")]
    [InlineData("vary-star")]
    [InlineData("set-cookie")]
    [InlineData("authorization")]
    [InlineData("custom-token")]
    public async Task CrawlAsync_RefreshDoesNotRetainNonReusableOrCredentialledResponses(string condition) {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool conditional = false;
        using HttpListener server = StartFlexibleServer(async context => {
            conditional |= context.Request.Headers["If-None-Match"] != null;
            context.Response.Headers["ETag"] = "\"version-one\"";
            if (condition == "no-store") context.Response.Headers["Cache-Control"] = "no-store";
            if (condition == "vary-star") context.Response.Headers["Vary"] = "*";
            if (condition == "set-cookie") context.Response.Headers["Set-Cookie"] = "session=validation; Path=/";
            await RespondAsync(context, "<main>Selected body</main><script>originalBodySentinel</script>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.Selector = "main";
            options.CacheResponses = true;
            options.OutputPath = source;
            if (condition == "authorization") options.Headers["Authorization"] = "Bearer validation-only";
            if (condition == "custom-token") options.Headers["X-Token"] = "validation-only";
            await HtmlCrawler.CrawlAsync(root, options);
            string manifest = File.ReadAllText(Path.Combine(source, "crawl-result.json"));
            Assert.DoesNotContain("\"HttpCache\"", manifest);
            Assert.DoesNotContain("originalBodySentinel", manifest);
            options.OutputPath = null;
            options.RefreshPath = source;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.False(conditional);
            Assert.False(page.ResponseRevalidated);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshSendsValidatorsOnlyToTheMatchingRedirectDestination() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string target = "/first";
        bool redirectWasConditional = false;
        bool secondWasConditional = false;
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            string? condition = context.Request.Headers["If-None-Match"];
            if (path == "/") {
                redirectWasConditional |= condition != null;
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = target;
            } else {
                if (path == "/second") secondWasConditional |= condition != null;
                context.Response.Headers["ETag"] = path == "/first" ? "\"first\"" : "\"second\"";
                if (path == "/first" && condition == "\"first\"") context.Response.StatusCode = 304;
                else await RespondAsync(context, "<main>" + path + " document</main>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = source;
            HtmlCrawlPage unchanged = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(unchanged.ResponseRevalidated);
            Assert.Equal(root + "first", unchanged.ResponseUrl);
            target = "/second";
            HtmlCrawlPage changed = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.False(changed.ResponseRevalidated);
            Assert.True(changed.ResponseChanged);
            Assert.Equal(root + "second", changed.ResponseUrl);
            Assert.Contains("/second document", changed.Text);
            Assert.False(redirectWasConditional);
            Assert.False(secondWasConditional);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_RefreshDownloadsAChangedRepresentationOrMismatched304Validator(bool mismatched304) {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool changed = false;
        int requests = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            requests++;
            context.Response.Headers["ETag"] = changed ? "\"new\"" : "\"old\"";
            if (mismatched304 && changed && context.Request.Headers["If-None-Match"] != null) {
                context.Response.StatusCode = 304;
            } else {
                await RespondAsync(context, changed ? "<main>New body</main>" : "<main>Old body</main>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            changed = true;
            options.OutputPath = null;
            options.RefreshPath = source;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            Assert.False(page.ResponseRevalidated);
            Assert.True(page.ResponseChanged);
            Assert.Equal(200, page.StatusCode);
            Assert.Equal("\"new\"", page.EntityTag);
            Assert.Contains("New body", page.Text);
            Assert.Equal(mismatched304 ? 3 : 2, requests);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshFailurePreservesItsSourceAndDoesNotReturnStaleContentAsSuccess() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool fail = false;
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.Headers["ETag"] = "\"original\"";
            if (fail) context.Response.StatusCode = 503;
            else await RespondAsync(context, "<main>Original body</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            string manifest = File.ReadAllText(Path.Combine(source, "crawl-result.json"));
            fail = true;
            options.OutputPath = null;
            options.RefreshPath = source;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
            Assert.Equal(503, page.StatusCode);
            Assert.False(page.ResponseRevalidated);
            Assert.Empty(page.Text);
            Assert.Equal(manifest, File.ReadAllText(Path.Combine(source, "crawl-result.json")));
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshDoesNotBypassASmallerResponseLimit() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool conditional = false;
        using HttpListener server = StartFlexibleServer(async context => {
            conditional |= context.Request.Headers["If-None-Match"] != null;
            context.Response.Headers["ETag"] = "\"large\"";
            await RespondAsync(context, "<main>" + new string('x', 500) + "</main>");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.OutputPath = source;
            await HtmlCrawler.CrawlAsync(root, options);
            options.OutputPath = null;
            options.RefreshPath = source;
            options.MaximumPageResponseBytes = 100;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
            Assert.False(conditional);
            Assert.False(page.ResponseRevalidated);
            Assert.Empty(page.Text);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_RefreshDoesNotRetainBodiesWhenAnEarlierRedirectSetsACookie() {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/") {
                context.Response.StatusCode = 302;
                context.Response.Headers["Set-Cookie"] = "session=validation; Path=/";
                context.Response.RedirectLocation = "/document";
            } else {
                context.Response.Headers["ETag"] = "\"session-body\"";
                await RespondAsync(context, "<main>Session body</main><script>originalBodySentinel</script>");
            }
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.CacheResponses = true;
            options.Selector = "main";
            options.OutputPath = source;
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
            string manifest = File.ReadAllText(Path.Combine(source, "crawl-result.json"));
            Assert.DoesNotContain("\"HttpCache\"", manifest);
            Assert.DoesNotContain("originalBodySentinel", manifest);
        } finally {
            if (Directory.Exists(source)) Directory.Delete(source, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrawlAsync_RefreshRejectsResumeOrOverwritingItsSourceBeforeWriting(bool resume) {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        string manifest = Path.Combine(source, "crawl-result.json");
        File.WriteAllText(manifest, "Source sentinel");
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.RefreshPath = source;
            if (resume) options.ResumePath = source;
            else options.OutputPath = source;
            await Assert.ThrowsAsync<ArgumentException>(() => HtmlCrawler.CrawlAsync("https://example.invalid/", options));
            Assert.Equal("Source sentinel", File.ReadAllText(manifest));
        } finally {
            Directory.Delete(source, true);
        }
    }
}
