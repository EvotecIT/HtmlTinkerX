Describe 'Colspan only table parsing' {
    It 'Parses table with colspan correctly - AgilityPack' {
        $Path = Join-Path $PSScriptRoot 'Documents/colspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content, $false, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Name | Should -Be 'Foo'
        $Table.Data[0].Value | Should -Be 'Foo'
        $Table.Data[0].Extra | Should -Be 'A'
        $Table.Data[1].Name | Should -Be 'Bar'
        $Table.Data[1].Value | Should -Be 'B'
        $Table.Data[1].Extra | Should -Be 'B'
    }
    It 'Parses table with colspan correctly - AngleSharp' {
        $Path = Join-Path $PSScriptRoot 'Documents/colspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithAngleSharpDetailed($Content, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Name | Should -Be 'Foo'
        $Table.Data[0].Value | Should -Be 'Foo'
        $Table.Data[0].Extra | Should -Be 'A'
        $Table.Data[1].Name | Should -Be 'Bar'
        $Table.Data[1].Value | Should -Be 'B'
        $Table.Data[1].Extra | Should -Be 'B'
    }
}
