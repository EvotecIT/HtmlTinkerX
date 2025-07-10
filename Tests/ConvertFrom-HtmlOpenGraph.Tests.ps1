Describe 'ConvertFrom-HtmlOpenGraph' {
    It 'Parses Open Graph data from content' {
        $path = Join-Path $PSScriptRoot 'Documents/open_graph.html'
        $content = Get-Content -LiteralPath $path -Raw
        $data = ConvertFrom-HtmlOpenGraph -Content $content
        $data.title | Should -Be 'Open Graph Title'
        $data.image | Should -Be 'https://example.com/img.png'
    }
}
