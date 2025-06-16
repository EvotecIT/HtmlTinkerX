Describe 'Rowspan only table parsing' {
    It 'Parses table with rowspan correctly - AgilityPack' {
        $Path = Join-Path $PSScriptRoot 'Documents/rowspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content, $false, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Item | Should -Be 'Bananas'
        $Table.Data[0].Q1 | Should -Be '1'
        $Table.Data[0].Q2 | Should -Be '2'
        $Table.Data[1].Item | Should -Be 'Bananas'
        $Table.Data[1].Q1 | Should -Be '3'
        $Table.Data[1].Q2 | Should -Be '4'
    }
    It 'Parses table with rowspan correctly - AngleSharp' {
        $Path = Join-Path $PSScriptRoot 'Documents/rowspan_only.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithAngleSharpDetailed($Content, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Item | Should -Be 'Bananas'
        $Table.Data[0].Q1 | Should -Be '1'
        $Table.Data[0].Q2 | Should -Be '2'
        $Table.Data[1].Item | Should -Be 'Bananas'
        $Table.Data[1].Q1 | Should -Be '3'
        $Table.Data[1].Q2 | Should -Be '4'
    }
}
