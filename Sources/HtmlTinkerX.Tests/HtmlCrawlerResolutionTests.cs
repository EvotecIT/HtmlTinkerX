using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_StaticAutoRenderDoesNotRequireAHeaderCapableBrowser() {
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context,
            "<main><p>" + string.Join(" ", Enumerable.Range(0, 50).Select(index => "Word" + index)) + "</p></main>"), out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.AutoRender = true; options.Browser = HtmlBrowserEngine.Firefox;
        options.Headers["X-Crawl-Secret"] = "test-secret";
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.False(page.Rendered);
        Assert.Equal(HtmlCrawlRenderMode.Static, page.RenderMode);
    }

    [Fact]
    public async Task CrawlAsync_OfflineAssetsUseDocumentBaseBeforeCanonicalRewriting() {
        ConcurrentQueue<string> requests = new();
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            requests.Enqueue(path);
            if (path == "/requested") context.Response.Redirect("/actual/page");
            else await RespondAsync(context, path == "/actual/page"
                ? "<head><base href='/media/'><link rel='canonical' href='/canonical/page'></head><main><p>Content</p><img src='image.png'></main>"
                : "image", path == "/actual/page" ? "text/html" : "image/png");
        }, out string root);
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(1);
            options.OutputPath = output; options.DownloadAssets = true; options.UseCanonicalUrls = true;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root + "requested", options);
            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(root + "canonical/page", page.Url);
            Assert.Equal(root + "media/", page.ResolutionBaseUrl);
            HtmlCrawlAsset asset = Assert.Single(result.Assets);
            Assert.Equal(root + "media/image.png", asset.Url);
            Assert.Contains(Path.GetFileName(asset.FilePath!), File.ReadAllText(page.HtmlPath!));
            Assert.DoesNotContain("/canonical/image.png", requests);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_RedirectedStylesheetsResolveNestedAssetsFromFinalUrl() {
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/style") context.Response.Redirect("/css/site.css");
            else await RespondAsync(context, path == "/" ? "<link rel='stylesheet' href='/style'><main>Content</main>"
                : path == "/css/site.css" ? "body { background: url('../images/background.png'); }" : "image",
                path == "/css/site.css" ? "text/css" : path == "/" ? "text/html" : "image/png");
        }, out string root);
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlOptions options = StaticOptions(1); options.OutputPath = output; options.DownloadAssets = true;
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Contains(result.Assets, asset => asset.Url == root + "images/background.png" && asset.Error == null);
            Assert.Equal(root + "css/site.css", result.Assets.Single(asset => asset.Url == root + "style").FinalUrl);
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }
}
