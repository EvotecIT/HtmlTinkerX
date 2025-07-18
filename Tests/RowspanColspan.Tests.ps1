Describe 'Rowspan/Colspan table parsing' {
    It 'Parses table with rowspan/colspan correctly - AgilityPack' {
        $Path = Join-Path $PSScriptRoot 'Documents/rowspan_colspan.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [HtmlTinkerX.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content, $false, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Item | Should -Be 'Apples'
        $Table.Data[0].Q1 | Should -Be '1'
        $Table.Data[0].Q2 | Should -Be '2'
        $Table.Data[0].Q3 | Should -Be '3'
        $Table.Data[1].Item | Should -Be 'Apples'
        $Table.Data[1].Q1 | Should -Be '4'
        $Table.Data[1].Q2 | Should -Be '4'
        $Table.Data[1].Q3 | Should -Be '5'
    }
    It 'Parses table with rowspan/colspan correctly - AngleSharp' {
        $Path = Join-Path $PSScriptRoot 'Documents/rowspan_colspan.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [HtmlTinkerX.HtmlParser]::ParseTablesWithAngleSharpDetailed($Content, $null, $null, $true)
        $Table = $Tables[0]
        $Table.Data.Count | Should -Be 2
        $Table.Data[0].Item | Should -Be 'Apples'
        $Table.Data[0].Q1 | Should -Be '1'
        $Table.Data[0].Q2 | Should -Be '2'
        $Table.Data[0].Q3 | Should -Be '3'
        $Table.Data[1].Item | Should -Be 'Apples'
        $Table.Data[1].Q1 | Should -Be '4'
        $Table.Data[1].Q2 | Should -Be '4'
        $Table.Data[1].Q3 | Should -Be '5'
    }
}
