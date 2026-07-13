using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlDiscoveryParserTests {
    [Fact]
    public void ParseLinks_ReturnsResolvedLinkTextAndContext() {
        const string html = """
<html>
  <body>
    <article>
      <p>Resolution attachment <a href="/files/resolution.pdf" title="Budget resolution.pdf">Download PDF</a></p>
      <p><a href="https://external.example.org/info">External info</a></p>
    </article>
  </body>
</html>
""";

        IReadOnlyList<HtmlDiscoveredLink> links = HtmlDiscoveryParser.ParseLinks(html, new Uri("https://bip.example.org/articles/1"));

        Assert.Equal(2, links.Count);
        Assert.Equal("https://bip.example.org/files/resolution.pdf", links[0].Url);
        Assert.Equal("Download PDF", links[0].Text);
        Assert.Equal("Budget resolution.pdf", links[0].Title);
        Assert.Contains("Resolution attachment", links[0].Context);
        Assert.False(links[0].IsExternal);
        Assert.True(links[1].IsExternal);
    }

    [Fact]
    public void ParseLinks_TreatsSchemeAndPortChangesAsExternalOrigins() {
        const string html = """
<a href="https://example.org/app">same origin</a>
<a href="http://example.org/app">different scheme</a>
<a href="https://example.org:8443/app">different port</a>
""";

        IReadOnlyList<HtmlDiscoveredLink> links = HtmlDiscoveryParser.ParseLinks(html, new Uri("https://example.org/root"));

        Assert.False(links[0].IsExternal);
        Assert.True(links[1].IsExternal);
        Assert.True(links[2].IsExternal);
    }

    [Fact]
    public void ParseLinks_RemovesStyleScriptAndSvgTextFromContext() {
        const string html = """
<html>
  <body>
    <article>
      <p>
        <style>.attachment-cls-1{fill:none;stroke-width:1.5px;}</style>
        <svg><title>Decorative icon</title><path d="M0 0" /></svg>
        <script>console.log("noise")</script>
        <a href="/api/attachments/18" title="Regulamin.pdf">Regulamin monitoringu.pdf</a>
        658.52 KB
      </p>
    </article>
  </body>
</html>
""";

        HtmlDiscoveredLink link = Assert.Single(HtmlDiscoveryParser.ParseLinks(html, new Uri("https://bip.example.org/articles/1")));

        Assert.Equal("Regulamin monitoringu.pdf", link.Text);
        Assert.Contains("658.52 KB", link.Context);
        Assert.DoesNotContain("attachment-cls", link.Context);
        Assert.DoesNotContain("Decorative icon", link.Context);
        Assert.DoesNotContain("console.log", link.Context);
    }

    [Fact]
    public void ParseSitemapUrls_ReturnsUrlsetAndSitemapIndexLocations() {
        const string xml = """
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>/sitemap-a.xml</loc></sitemap>
  <sitemap><loc>https://example.org/sitemap-b.xml</loc></sitemap>
</sitemapindex>
""";

        IReadOnlyList<string> urls = HtmlDiscoveryParser.ParseSitemapUrls(xml, new Uri("https://example.org/root/sitemap.xml"));

        Assert.Equal(new[] {
            "https://example.org/sitemap-a.xml",
            "https://example.org/sitemap-b.xml"
        }, urls);
    }

    [Fact]
    public void ParseSyndicationItems_ReturnsRssItems() {
        const string xml = """
<rss version="2.0">
  <channel>
    <item>
      <title>Road works</title>
      <link>/roads/1</link>
      <description>Temporary traffic organization</description>
      <pubDate>Mon, 11 May 2026 10:00:00 GMT</pubDate>
    </item>
  </channel>
</rss>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(
            xml,
            new Uri("https://example.org/feed/"),
            "https://example.org/feed/");

        HtmlSyndicationItem item = Assert.Single(items);
        Assert.Equal("Road works", item.Title);
        Assert.Equal("https://example.org/roads/1", item.Url);
        Assert.Equal("Temporary traffic organization", item.Summary);
        Assert.Equal("https://example.org/feed/", item.SourceFeedUrl);
        Assert.NotNull(item.Published);
    }
}
