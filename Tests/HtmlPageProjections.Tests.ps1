BeforeAll {
    Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force
}

Describe 'Page reader optional projections' {
    BeforeAll {
        $script:Html = '<html lang="en"><head><title>Status</title></head><body><main><h1>Status</h1><p>Services are healthy.</p><a href="details">Details</a></main></body></html>'
    }

    It 'keeps semantic objects when optional projections are skipped' {
        $page = Get-HtmlPage -Content $script:Html -BaseUrl 'https://example.org/' -NoReadableText -NoMarkdown -NoWebData -NoCollections

        $page.Title | Should -Be 'Status'
        $page.Language | Should -Be 'en'
        $page.Headings | Should -HaveCount 1
        $page.Paragraphs.Text | Should -Contain 'Services are healthy.'
        $page.ReadableText.Text | Should -BeNullOrEmpty
        $page.Markdown | Should -BeNullOrEmpty
        $page.Links | Should -HaveCount 0
        $page.Collections | Should -HaveCount 0
    }

    It 'applies projection selection to a rendered snapshot without changing its URLs' {
        $snapshot = [HtmlTinkerX.HtmlRenderedPageSnapshot]::new()
        $snapshot.Html = $script:Html
        $snapshot.Url = 'https://example.org/start'
        $snapshot.FinalUrl = 'https://example.org/reports/'

        $page = Get-HtmlPage -RenderedSnapshot $snapshot -NoMarkdown -NoCollections

        $page.SourceUrl | Should -Be $snapshot.Url
        $page.FinalUrl | Should -Be $snapshot.FinalUrl
        $page.Markdown | Should -BeNullOrEmpty
        $page.ReadableText.Text | Should -Not -BeNullOrEmpty
        $page.Links[0].Url | Should -Be 'https://example.org/reports/details'
    }
}
