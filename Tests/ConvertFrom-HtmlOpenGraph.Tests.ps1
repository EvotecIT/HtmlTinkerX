Describe 'ConvertFrom-HtmlOpenGraph' {
    It 'Parses Open Graph data from content' {
        $path = Join-Path $PSScriptRoot 'Documents/open_graph.html'
        $content = Get-Content -LiteralPath $path -Raw
        $data = ConvertFrom-HtmlOpenGraph -Content $content
        $data.title | Should -Be 'Open Graph Title'
        $data.image | Should -Be 'https://example.com/img.png'
    }

    It 'Parses Open Graph data from selected HtmlNode pipeline input' {
        $path = Join-Path $PSScriptRoot 'Documents/open_graph.html'
        $content = Get-Content -LiteralPath $path -Raw
        $data = ConvertFrom-HTML -Content $content |
            Select-HtmlNode -XPath '//head' |
            ConvertFrom-HtmlOpenGraph

        $data.title | Should -Be 'Open Graph Title'
        $data.image | Should -Be 'https://example.com/img.png'
    }
}
