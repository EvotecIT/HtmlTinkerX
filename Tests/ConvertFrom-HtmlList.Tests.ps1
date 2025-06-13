Describe 'ConvertFrom-HtmlList' {
    It 'Parses sample lists using AgilityPack' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -Engine AgilityPack
        $lists.Count | Should -Be 2
        $lists[0][0].Column1 | Should -Be 'Item1'
        $lists[1][1].Column1 | Should -Be 'Second'
    }

    It 'Parses sample lists using AngleSharp' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -Engine AngleSharp
        $lists.Count | Should -Be 2
        $lists[0][0].Column1 | Should -Be 'Item1'
        $lists[1][1].Column1 | Should -Be 'Second'
    }

    It 'Returns strings when AsString is used' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -AsString
        $lists[0] | Should -Be @('Item1','Item2')
        $lists[1] | Should -Be @('First','Second')
    }

    It 'Parses sample lists with metadata and objects' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_lists.html'
        $content = Get-Content -LiteralPath $path -Raw
        $lists = ConvertFrom-HtmlList -Content $content -IncludeMetadata
        $lists.Count | Should -Be 2
        $lists[0].Data[0].Column1 | Should -Be 'Item1'
        $lists[0].ListIndex | Should -Be 0
    }

    It 'Returns single list when only one list is found' {
        $path = Join-Path $PSScriptRoot 'Documents/single_list.html'
        $content = Get-Content -LiteralPath $path -Raw
        $list = ConvertFrom-HtmlList -Content $content -Engine AgilityPack
        $list.Count | Should -Be 2
        $list[0].Column1 | Should -Be 'Item1'
    }
}
