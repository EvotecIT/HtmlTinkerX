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

    [Fact]
    public void ParseSyndicationItems_ReadsNonPermaLinkGuidAndCategoriesWhenEveryItemSharesOneLink() {
        // Shaped like a cloud status feed: every item links to the same status page, so only the guid tells items apart.
        const string xml = """
<rss version="2.0">
  <channel>
    <title>Status - Active incidents</title>
    <link>https://status.example.org/</link>
    <lastBuildDate>Tue, 29 Sep 2026 14:05:00 GMT</lastBuildDate>
    <item>
      <title>Mitigated - Storage in West Europe</title>
      <link>https://status.example.org/status/</link>
      <guid isPermaLink="false">ABCD-12X</guid>
      <category>Storage</category>
      <category>West Europe</category>
      <category>storage</category>
      <description>Customers may have experienced errors.</description>
      <pubDate>Tue, 29 Sep 2026 13:40:00 GMT</pubDate>
    </item>
    <item>
      <title>Active - Compute in East US</title>
      <link>https://status.example.org/status/</link>
      <guid isPermaLink="FALSE"> EFGH-34Y </guid>
      <category>Virtual Machines</category>
    </item>
  </channel>
</rss>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(xml);

        Assert.Equal(2, items.Count);
        Assert.Equal("ABCD-12X", items[0].Id);
        Assert.False(items[0].IdIsPermaLink);
        Assert.Equal(new[] { "Storage", "West Europe" }, items[0].Categories);
        Assert.Equal("https://status.example.org/status/", items[0].Url);
        Assert.Equal("Customers may have experienced errors.", items[0].Summary);
        Assert.Null(items[0].Content);
        Assert.Equal("EFGH-34Y", items[1].Id);
        Assert.False(items[1].IdIsPermaLink);
        Assert.Equal(new[] { "Virtual Machines" }, items[1].Categories);
    }

    [Fact]
    public void ParseSyndicationItems_ReadsPermaLinkGuidAndContentEncoded() {
        // Shaped like an incident.io status feed: a guid, a status-prefixed description and the full body in content:encoded.
        const string xml = """
<rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
  <channel>
    <title>Example status</title>
    <item>
      <title>Elevated errors on the API</title>
      <link>https://status.example.com/incidents/01J</link>
      <guid>https://status.example.com/incidents/01J</guid>
      <description>Status: Resolved

Affected components: API, Batch</description>
      <content:encoded><![CDATA[<p><strong>Resolved</strong> - Error rates are back to normal.</p>]]></content:encoded>
      <pubDate>Mon, 28 Sep 2026 09:15:00 +0000</pubDate>
    </item>
    <item>
      <title>No guid at all</title>
      <link>https://status.example.com/incidents/02K</link>
    </item>
  </channel>
</rss>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(xml);

        Assert.Equal("https://status.example.com/incidents/01J", items[0].Id);
        Assert.True(items[0].IdIsPermaLink);
        Assert.StartsWith("Status: Resolved", items[0].Summary);
        Assert.Equal("<p><strong>Resolved</strong> - Error rates are back to normal.</p>", items[0].Content);
        Assert.Empty(items[0].Categories);
        Assert.Null(items[1].Id);
        Assert.False(items[1].IdIsPermaLink);
        Assert.Null(items[1].Content);
    }

    [Fact]
    public void ParseSyndicationItems_KeepsGuidUrlFallbackWhenLinkIsMissing() {
        const string xml = """
<rss version="2.0">
  <channel>
    <item>
      <title>Guid only</title>
      <guid>https://example.org/posts/7</guid>
    </item>
  </channel>
</rss>
""";

        HtmlSyndicationItem item = Assert.Single(HtmlDiscoveryParser.ParseSyndicationItems(xml));

        Assert.Equal("https://example.org/posts/7", item.Url);
        Assert.Equal("https://example.org/posts/7", item.Id);
        Assert.True(item.IdIsPermaLink);
    }

    [Fact]
    public void ParseSyndicationItems_ReadsAtomIdPublishedUpdatedCategoriesAndHtmlContent() {
        // Shaped like a Statuspage history feed: tag: ids, separate published and updated, and the update trail as HTML content.
        const string xml = """
<feed xmlns="http://www.w3.org/2005/Atom" xml:lang="en-US">
  <id>tag:status.example.com,2005:/history</id>
  <link rel="alternate" type="text/html" href="https://status.example.com" />
  <link rel="self" type="application/atom+xml" href="https://status.example.com/history.atom" />
  <title>Example Status - Incident History</title>
  <updated>2026-09-29T12:30:00Z</updated>
  <entry>
    <id>tag:status.example.com,2005:Incident/4242</id>
    <published>2026-09-29T10:00:00Z</published>
    <updated>2026-09-29T12:30:00Z</updated>
    <link rel="alternate" type="text/html" href="https://status.example.com/incidents/abc" />
    <title>Elevated errors on Example API</title>
    <category term="API" label="Public API" />
    <category label="Console" />
    <content type="html">&lt;p&gt;&lt;strong&gt;Resolved&lt;/strong&gt; - Fixed.&lt;/p&gt;&lt;p&gt;&lt;strong&gt;Investigating&lt;/strong&gt; - Looking into it.&lt;/p&gt;</content>
  </entry>
  <entry>
    <id>urn:uuid:1225c695-cfb8-4ebb-aaaa-80da344efa6a</id>
    <link href="https://example.org/entry/2" />
    <title>Xhtml entry</title>
    <summary>Short summary</summary>
    <content type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml"><p>Body <em>text</em></p></div></content>
  </entry>
</feed>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(xml);

        Assert.Equal("tag:status.example.com,2005:Incident/4242", items[0].Id);
        Assert.False(items[0].IdIsPermaLink);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero), items[0].Published);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 12, 30, 0, TimeSpan.Zero), items[0].Updated);
        Assert.Equal(new[] { "API", "Console" }, items[0].Categories);
        Assert.Equal("<p><strong>Resolved</strong> - Fixed.</p><p><strong>Investigating</strong> - Looking into it.</p>", items[0].Content);
        // Summary keeps its existing fallback to content when an entry has no summary.
        Assert.Equal(items[0].Content, items[0].Summary);

        Assert.Equal("urn:uuid:1225c695-cfb8-4ebb-aaaa-80da344efa6a", items[1].Id);
        Assert.Equal("Short summary", items[1].Summary);
        Assert.Contains("<p", items[1].Content);
        Assert.Contains("Body <em", items[1].Content);
        Assert.DoesNotContain("<div", items[1].Content);
    }

    [Fact]
    public void ParseSyndicationFeed_ReturnsRssChannelDetailsAndLastBuildDate() {
        const string xml = """
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <atom:link href="https://example.org/feed.xml" rel="self" type="application/rss+xml" />
    <title>Example news</title>
    <link>/news/</link>
    <pubDate>Mon, 28 Sep 2026 08:00:00 GMT</pubDate>
    <lastBuildDate>Tue, 29 Sep 2026 14:05:00 GMT</lastBuildDate>
    <item><title>One</title><link>/news/1</link></item>
  </channel>
</rss>
""";

        HtmlSyndicationFeed feed = HtmlDiscoveryParser.ParseSyndicationFeed(xml, new Uri("https://example.org/feed.xml"), "https://example.org/feed.xml");

        Assert.Equal("Example news", feed.Title);
        Assert.Equal("https://example.org/news/", feed.Url);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 14, 5, 0, TimeSpan.Zero), feed.Updated);
        Assert.Equal("https://example.org/feed.xml", feed.SourceFeedUrl);
        HtmlSyndicationItem item = Assert.Single(feed.Items);
        Assert.Equal("https://example.org/news/1", item.Url);
        Assert.Equal("https://example.org/feed.xml", item.SourceFeedUrl);
    }

    [Fact]
    public void ParseSyndicationFeed_FallsBackToChannelPubDateThenDublinCoreDate() {
        const string pubDateOnly = """
<rss version="2.0"><channel><title>A</title><pubDate>Mon, 28 Sep 2026 08:00:00 GMT</pubDate></channel></rss>
""";
        const string rdf = """
<rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns="http://purl.org/rss/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
  <channel rdf:about="https://example.org/">
    <title>RDF feed</title>
    <link>https://example.org/</link>
    <dc:date>2026-09-27T07:00:00Z</dc:date>
  </channel>
  <item rdf:about="https://example.org/1"><title>One</title><link>https://example.org/1</link><dc:date>2026-09-26T07:00:00Z</dc:date></item>
</rdf:RDF>
""";

        Assert.Equal(new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero), HtmlDiscoveryParser.ParseSyndicationFeed(pubDateOnly).Updated);
        HtmlSyndicationFeed feed = HtmlDiscoveryParser.ParseSyndicationFeed(rdf);
        Assert.Equal("RDF feed", feed.Title);
        Assert.Equal("https://example.org/", feed.Url);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 7, 0, 0, TimeSpan.Zero), feed.Updated);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 7, 0, 0, TimeSpan.Zero), Assert.Single(feed.Items).Published);
    }

    [Fact]
    public void ParseSyndicationFeed_ReturnsAtomFeedUpdated() {
        const string xml = """
<feed xmlns="http://www.w3.org/2005/Atom">
  <title>Atom feed</title>
  <link rel="self" href="https://example.org/feed.atom" />
  <link rel="alternate" href="https://example.org/" />
  <updated>2026-09-29T12:30:00+02:00</updated>
</feed>
""";

        HtmlSyndicationFeed feed = HtmlDiscoveryParser.ParseSyndicationFeed(xml);

        Assert.Equal("Atom feed", feed.Title);
        Assert.Equal("https://example.org/", feed.Url);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 10, 30, 0, TimeSpan.Zero), feed.Updated);
        Assert.Empty(feed.Items);
    }

    [Fact]
    public void ParseSyndicationItems_ParsesRfc822ZoneNamesAndWrongWeekdays() {
        const string xml = """
<rss version="2.0">
  <channel>
    <item><title>Zone name</title><link>https://example.org/1</link><pubDate>Tue, 29 Sep 2026 10:00:00 PDT</pubDate></item>
    <item><title>Wrong weekday</title><link>https://example.org/2</link><pubDate>Fri, 29 Sep 2026 10:00:00 GMT</pubDate></item>
    <item><title>Unparseable</title><link>https://example.org/3</link><pubDate>sometime soon</pubDate></item>
  </channel>
</rss>
""";

        IReadOnlyList<HtmlSyndicationItem> items = HtmlDiscoveryParser.ParseSyndicationItems(xml);

        Assert.Equal(new DateTimeOffset(2026, 9, 29, 17, 0, 0, TimeSpan.Zero), items[0].Published);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero), items[1].Published);
        Assert.Null(items[2].Published);
    }
}
