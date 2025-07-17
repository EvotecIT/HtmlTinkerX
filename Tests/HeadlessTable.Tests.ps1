Describe 'Headless table parsing' {
    It 'Parses tables without headers and generates column names' {
        $Path = Join-Path $PSScriptRoot 'Documents/headless_table.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [HtmlTinkerX.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content, $false, $null, $null, $true)

        $Tables.Count | Should -BeGreaterOrEqual 1
        $First = $Tables[0]
        $First.Data.Count | Should -BeGreaterThan 0
        $First.Metadata.Headers[0] | Should -Be 'Column1'
    }
}
