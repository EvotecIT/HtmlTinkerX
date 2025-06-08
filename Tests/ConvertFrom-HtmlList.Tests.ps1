Describe 'ConvertFrom-HtmlList' {
    It 'Parses sample lists using AgilityPack' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -Engine AgilityPack
        $lists.Count | Should -Be 2
        $lists[0] | Should -Be @('Item1','Item2')
        $lists[1] | Should -Be @('First','Second')
    }

    It 'Parses sample lists using AngleSharp' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -Engine AngleSharp
        $lists.Count | Should -Be 2
        $lists[0] | Should -Be @('Item1','Item2')
        $lists[1] | Should -Be @('First','Second')
    }
}
