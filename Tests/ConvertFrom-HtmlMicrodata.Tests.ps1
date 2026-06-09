Describe 'ConvertFrom-HtmlMicrodata' {
    It 'Parses microdata items from content' {
        $path = Join-Path $PSScriptRoot 'Documents/microdata.html'
        $content = Get-Content -LiteralPath $path -Raw
        $items = ConvertFrom-HtmlMicrodata -Content $content
        $items.Count | Should -Be 1
        $items[0].Properties.name[0] | Should -Be 'Jane Doe'
    }

    It 'Parses microdata items from selected HtmlNode pipeline input' {
        $path = Join-Path $PSScriptRoot 'Documents/microdata.html'
        $content = Get-Content -LiteralPath $path -Raw
        $items = ConvertFrom-HTML -Content $content |
            Select-HtmlNode -XPath '//*[@itemscope]' |
            ConvertFrom-HtmlMicrodata

        $items.Count | Should -Be 1
        $items[0].Properties.name[0] | Should -Be 'Jane Doe'
    }
}
