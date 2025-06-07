Describe 'AzureStatus HTML parsing' {
    It 'ConvertFrom-HtmlTableDetailed parses tables with headers' {
        $Path = Join-Path $PSScriptRoot 'Documents/azure_status.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = [PSParseHTML.HtmlParser]::ParseTablesWithHtmlAgilityPackDetailed($Content)
        $DataTables = $Tables | Where-Object { $_.Metadata.RowCount -gt 1 }

        $DataTables.Count | Should -BeGreaterOrEqual 7

        foreach ($table in $DataTables) {
            $table.Metadata.ColumnCount | Should -BeGreaterOrEqual 4
            $table.Metadata.Headers | Should -Contain 'Products and services'
        }

        $First = $DataTables[0]
        $First.Metadata.Headers | Should -Contain '*Non-Regional'
        $First.Metadata.Headers | Should -Contain 'East US'
        $First.Data[0].'Products and services' | Should -Be 'Compute'
        $First.Data[1].'Products and services' | Should -Not -BeNullOrEmpty
        $First.Data[1].'East US' | Should -Not -BeNullOrEmpty
        $First.Data[1].'East US 2' | Should -Not -BeNullOrEmpty
        $First.Data[2].'Products and services' | Should -Not -BeNullOrEmpty
        $First.Data[2].'East US' | Should -Not -BeNullOrEmpty
        $First.Data[2].'East US 2' | Should -Not -BeNullOrEmpty
    }
}
