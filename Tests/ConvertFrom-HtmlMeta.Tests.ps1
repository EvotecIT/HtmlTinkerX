Describe 'ConvertFrom-HtmlMeta' {
    It 'Parses meta tags from content' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_meta.html'
        $content = Get-Content -LiteralPath $path -Raw
        $tags = ConvertFrom-HtmlMeta -Content $content
        $tags.Count | Should -Be 2
        ($tags | Where-Object Name -eq 'description').Content | Should -Be 'Example site'
    }

    It 'Parses meta tags from selected HtmlNode pipeline input' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_meta.html'
        $content = Get-Content -LiteralPath $path -Raw
        $tags = ConvertFrom-HTML -Content $content |
            Select-HtmlNode -XPath '//head' |
            ConvertFrom-HtmlMeta

        $tags.Count | Should -Be 2
        ($tags | Where-Object Name -eq 'description').Content | Should -Be 'Example site'
    }
}
