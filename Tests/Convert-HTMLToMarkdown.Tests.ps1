Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Convert-HTMLToMarkdown' {
    It 'converts HTML content to Markdown and resolves relative URLs' {
        $html = @'
<main>
  <h1>Hello</h1>
  <p>Read the <a href="/docs/start">docs</a>.</p>
</main>
'@

        $markdown = Convert-HTMLToMarkdown -Content $html -PageUrl 'https://example.com/app/page.html'

        $markdown | Should -Match '# Hello'
        $markdown | Should -Match '\[docs\]\(https://example\.com/docs/start\)'
    }

    It 'supports rich image Markdown mode' {
        $html = @'
<main>
  <figure>
    <a href="/gallery/hero" title="Open photo">
      <img src="/img/hero.png" alt="Hero" title="View hero" width="256" height="128" />
    </a>
  </figure>
</main>
'@

        $markdown = Convert-HTMLToMarkdown -Content $html -PageUrl 'https://example.com/app/page.html' -MarkdownImageMode RichMarkdown

        $markdown | Should -Match '\[!\[Hero\]\(https://example\.com/img/hero\.png "View hero"\)\]\(https://example\.com/gallery/hero "Open photo"\)\{width=256 height=128\}'
    }
}
