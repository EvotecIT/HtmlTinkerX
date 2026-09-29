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

    [Fact]
    public void ParseSyndicationItems_ReadsDublinCoreDatesWhenRssHasNoPubDate() {
        const string xml = """
<rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/">
  <channel>
    <item>
      <title>Day-one support</title>
      <link>https://example.org/blog/1</link>
      <dc:date>2026-09-22T20:55:00+00:00</dc:date>
      <dcterms:modified>2026-09-23T08:00:00+00:00</dcterms:modified>
    </item>
    <item>
      <title>Only created</title>
      <link>https://example.org/blog/2</link>
      <dcterms:created>2026-09-20T10:00:00Z</dcterms:created>
    </item>
    <item>
      <title>Unrelated date element</title>
      <link>https://example.org/blog/3</link>
      <date>2026-01-01</date>
    </item>
  </channel>
</rss>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(xml);

        Assert.Equal(new DateTimeOffset(2026, 9, 22, 20, 55, 0, TimeSpan.Zero), items[0].Published);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.Zero), items[0].Updated);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), items[1].Published);
        // A bare date element outside the Dublin Core namespaces is not trusted as a publication date.
        Assert.Null(items[2].Published);
    }

    [Fact]
    public void ParseSyndicationItems_ReadsAtom03IssuedAndModified() {
        const string xml = """
<feed xmlns="http://purl.org/atom/ns#">
  <entry>
    <title>Old-style entry</title>
    <link rel="alternate" href="https://example.org/entry/1" />
    <issued>2026-09-01T12:00:00Z</issued>
    <modified>2026-09-02T12:00:00Z</modified>
  </entry>
</feed>
""";

        HtmlSyndicationItem item = Assert.Single(HtmlDiscoveryParser.ParseSyndicationItems(xml));

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), item.Published);
        Assert.Equal(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero), item.Updated);
    }
}
