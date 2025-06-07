Describe 'AzureStatus HTML parsing' {
    It 'ConvertFrom-HtmlTableDetailed parses tables with headers' {
        $Path = Join-Path $PSScriptRoot 'Documents/azure_status.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content)
        $DataTables = $Tables | Where-Object { $_.Metadata.RowCount -gt 1 }

        $DataTables.Count | Should -BeGreaterThanOrEqual 7

        foreach ($table in $DataTables) {
            $table.Metadata.ColumnCount | Should -BeGreaterThanOrEqual 4
            $table.Metadata.Headers | Should -Contain 'Products and services'
        }

        $First = $DataTables[0]
        $First.Metadata.Headers | Should -Contain '*Non-Regional'
        $First.Metadata.Headers | Should -Contain 'East US'
        $First.Data[0].'Products and services' | Should -Be 'Compute'
    }
}
