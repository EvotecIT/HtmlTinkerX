Describe 'HTML discovery cmdlets' {
    It 'returns readable text and metadata from article-like content' {
        $html = @'
<html>
  <head><title>Site shell</title></head>
  <body>
    <nav>Home Archive Contact</nav>
    <main>
      <article id="notice">
        <h1>Road works notice</h1>
        <p>The public road will be rebuilt during summer.</p>
        <p>Attachments include a map PDF and schedule.</p>
      </article>
    </main>
  </body>
</html>
'@

        $metadata = Convert-HTMLToText -Content $html -Readable -IncludeMetadata
        $text = Convert-HTMLToText -Content $html -Readable

        $metadata.Title | Should -Be 'Road works notice'
        $metadata.Text | Should -Match 'public road'
        $metadata.SelectorHint | Should -Be 'article#notice'
        $metadata.CandidateCount | Should -BeGreaterThan 0
        $text | Should -Match 'Attachments include'
    }

    It 'extracts links with resolved urls and context' {
        $html = @'
<html><body><article>
  <p>Resolution attachment <a href="/files/resolution.pdf" title="Budget resolution.pdf">Download PDF</a></p>
  <p><a href="https://external.example.org/info">External info</a></p>
</article></body></html>
'@

        $links = ConvertFrom-HtmlLink -Content $html -BaseUrl 'https://bip.example.org/articles/1'

        $links.Count | Should -Be 2
        $links[0].Url | Should -Be 'https://bip.example.org/files/resolution.pdf'
        $links[0].Title | Should -Be 'Budget resolution.pdf'
        $links[0].Context | Should -Match 'Resolution attachment'
        $links[0].IsExternal | Should -BeFalse
        $links[1].IsExternal | Should -BeTrue
    }

    It 'extracts sitemap URLs from sitemap index XML' {
        $xml = @'
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>/sitemap-a.xml</loc></sitemap>
  <sitemap><loc>https://example.org/sitemap-b.xml</loc></sitemap>
</sitemapindex>
'@

        $urls = ConvertFrom-HtmlSitemap -Content $xml -BaseUrl 'https://example.org/root/sitemap.xml'

        $urls | Should -Be @(
            'https://example.org/sitemap-a.xml'
            'https://example.org/sitemap-b.xml'
        )
    }

    It 'extracts syndication feed items' {
        $xml = @'
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
'@

        $item = ConvertFrom-HtmlSyndication -Content $xml -BaseUrl 'https://example.org/feed/' -SourceFeedUrl 'https://example.org/feed/'

        $item.Title | Should -Be 'Road works'
        $item.Url | Should -Be 'https://example.org/roads/1'
        $item.Summary | Should -Be 'Temporary traffic organization'
        $item.SourceFeedUrl | Should -Be 'https://example.org/feed/'
        $item.Published | Should -Not -BeNullOrEmpty
    }
}
