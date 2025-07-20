Describe 'Colspan only table parsing' {
    It 'Parses table with colspan correctly - AgilityPack' {
        $Path = Join-Path $PSScriptRoot 'Documents/colspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [HtmlTinkerX.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content, $false, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Values.Name | Should -Be 'Foo'
        $Table.Data[0].Values.Value | Should -Be 'Foo'
        $Table.Data[0].Values.Extra | Should -Be 'A'
        $Table.Data[1].Values.Name | Should -Be 'Bar'
        $Table.Data[1].Values.Value | Should -Be 'B'
        $Table.Data[1].Values.Extra | Should -Be 'B'
    }
    It 'Parses table with colspan correctly - AngleSharp' {
        $Path = Join-Path $PSScriptRoot 'Documents/colspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [HtmlTinkerX.HtmlParser]::ParseTablesWithAngleSharpDetailed($Content, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Values.Name | Should -Be 'Foo'
        $Table.Data[0].Values.Value | Should -Be 'Foo'
        $Table.Data[0].Values.Extra | Should -Be 'A'
        $Table.Data[1].Values.Name | Should -Be 'Bar'
        $Table.Data[1].Values.Value | Should -Be 'B'
        $Table.Data[1].Values.Extra | Should -Be 'B'
    }
}
