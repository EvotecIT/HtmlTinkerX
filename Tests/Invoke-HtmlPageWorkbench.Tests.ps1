Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force
. "$PSScriptRoot/Support/HtmlRedirectTestServer.ps1"

Describe 'Invoke-HtmlPageWorkbench' {
    It 'exports the unified page workbench command' {
        Get-Command Invoke-HtmlPageWorkbench | Should -Not -BeNullOrEmpty
    }

    It 'returns grouped static page intelligence from content' {
        $html = @'
<html>
<head>
<title>Workbench Demo</title>
<meta property="og:title" content="Workbench Demo" />
<script type="application/ld+json">{"@context":"https://schema.org","@type":"Article","headline":"Workbench Demo"}</script>
<script>window.__CONFIG__ = { api: { baseUrl: "/api" } }; fetch("/api/items");</script>
</head>
<body>
<main>
<h1>Workbench Demo</h1>
<p>This page has enough readable content to prove that one command returns text, markdown, data, forms, and endpoints.</p>
<a href="/docs">Docs</a>
<img src="/hero.png" alt="Hero" />
<form method="post" action="/login">
<input type="hidden" name="token" value="secret" />
<input name="user" />
</form>
</main>
</body>
</html>
'@

        $result = Invoke-HtmlPageWorkbench -Content $html -BaseUrl 'https://example.org/page' -NoHtml

        $result.GetType().Name | Should -Be 'HtmlPageWorkbenchResult'
        $result.SourceUrl | Should -Be 'https://example.org/page'
        $result.Html | Should -Be ''
        $result.ReadableText.Text | Should -Match 'one command returns'
        $result.Markdown | Should -Match 'Workbench Demo'
        $result.Forms.Count | Should -Be 1
        $result.HiddenFields.Count | Should -Be 1
        $result.Links.Count | Should -Be 1
        $result.JsonLd.Count | Should -Be 1
        $result.OpenGraph.Count | Should -Be 1
        $result.EndpointCount | Should -BeGreaterThan 0
        $result.JavaScriptConfigCount | Should -BeGreaterThan 0
        $result.Warnings -join "`n" | Should -Match 'Hidden fields'
        $result.SuggestedNextCommand | Should -Not -BeNullOrEmpty
    }

    It 'uses a rendered snapshot as the primary workbench view' {
        $staticHtml = @'
<html>
<head><title>Loading</title><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
'@
        $renderedHtml = @'
<html>
<head><title>Rendered App</title></head>
<body>
<main>
<h1>Rendered App</h1>
<p>The rendered application now exposes real content and navigation links.</p>
<a href="/ready">Ready</a>
<form method="post" action="/submit"><input type="hidden" name="csrf" value="token" /></form>
</main>
</body>
</html>
'@
        $snapshot = [HtmlTinkerX.HtmlRenderedPageSnapshot]::new()
        $snapshot.Url = 'https://example.org/app'
        $snapshot.FinalUrl = 'https://example.org/app#ready'
        $snapshot.Title = 'Rendered App'
        $snapshot.Html = $renderedHtml
        $snapshot.Markdown = "# Rendered App`n`nThe rendered application now exposes real content and navigation links."

        $result = Invoke-HtmlPageWorkbench -Content $staticHtml -BaseUrl 'https://example.org/app' -RenderedSnapshot $snapshot

        $result.AnalysisMode | Should -Be 'RenderedSnapshot'
        $result.FinalUrl | Should -Be 'https://example.org/app#ready'
        $result.RenderedSnapshot | Should -Not -BeNullOrEmpty
        $result.StaticRenderedComparison | Should -Not -BeNullOrEmpty
        $result.Links.Count | Should -Be 1
        $result.Forms.Count | Should -Be 1
        $result.HiddenFields.Count | Should -Be 1
        $result.StaticData.Where({ $_.Kind -eq 'Link' }).Count | Should -Be 0
        $result.Warnings -join "`n" | Should -Match 'Rendered content differs'
    }

    It 'uses the final response Url as the base when Url follows redirects' {
        $server = [HtmlRedirectTestServer]::new()
        try {
            $result = Invoke-HtmlPageWorkbench -Url ($server.Url + 'redirect-workbench')

            $result.SourceUrl | Should -Be ($server.Url + 'final/workbench')
            $result.Links[0].Value | Should -Be ($server.Url + 'final/relative-link')
            ($result.ApiEndpoints | Where-Object ResolvedUrl -EQ ($server.Url + 'final/relative-api')) | Should -Not -BeNullOrEmpty
        } finally {
            $server.Dispose()
        }
    }
}
