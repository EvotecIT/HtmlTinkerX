using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
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
                context.Response.RedirectLocation = "/document?utm_source=one&version=2";
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
                : "<main>Initial document</main><button id='continue' onclick=\"location.href='/document?version=2'\">Continue</button>");
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
}
