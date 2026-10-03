using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrawlAsync_NestedSitemapsRespectScopeAndDoNotInheritCredentials(bool restrict) {
        ConcurrentQueue<(string? Auth, string? Secret)> external = new();
        using HttpListener other = StartFlexibleServer(async context => {
            external.Enqueue((context.Request.Headers["Authorization"], context.Request.Headers["X-Crawl-Secret"]));
            await RespondAsync(context, "<urlset />", "application/xml");
        }, out string otherRoot, "localhost");
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/index") {
                context.Response.Redirect("/actual/index.xml");
            } else {
                await RespondAsync(context, path == "/actual/index.xml"
                    ? $"<sitemapindex><sitemap><loc>{otherRoot}nested.xml</loc></sitemap><sitemap><loc>nested.xml</loc></sitemap></sitemapindex>"
                    : path == "/actual/nested.xml" ? "<urlset><url><loc>found</loc></url></urlset>"
                    : "<main>content</main>", path.EndsWith(".xml") ? "application/xml" : "text/html");
            }
        }, out string root, "127.0.0.1");
        HtmlCrawlOptions options = StaticOptions(3);
        options.RestrictToHost = restrict; options.SitemapUrls.Add(root + "index");
        options.Username = "test-user"; options.Password = "test-password"; options.Headers["X-Crawl-Secret"] = "test-secret";
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Contains(result.Pages, page => page.Url == root + "actual/found");
        if (restrict) Assert.Empty(external);
        else {
            Assert.Single(external);
            Assert.All(external, request => { Assert.Null(request.Auth); Assert.Null(request.Secret); });
        }
    }
}
