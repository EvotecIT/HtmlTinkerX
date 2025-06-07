Describe -Name 'ConvertFrom-HtmlTable' {
    It 'Given a HTML table online with polish chars - Should convert it to a PowerShell object' {
        $Url = 'https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm'

        $AllTables = ConvertFrom-HtmlTable -Url $Url -Engine AgilityPack
        $AllTables.Count | Should -BeGreaterThan 0
    }
    It 'Given a HTML Page with Tables' {
        $AllTables = ConvertFrom-HtmlTable -Url 'https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference'
        $AllTables.Count | Should -BeGreaterThan 0
    }

    It 'ConvertFrom-HTML cmdlet parses example.com' {
        $doc = ConvertFrom-HTML -Url 'https://example.com'
        $doc | Should -Not -BeNullOrEmpty
    }

    It 'Parses azure_status.html with AgilityPack and metadata' {
        $Path = Join-Path $PSScriptRoot 'Documents/azure_status.html'
        $Content = Get-Content -LiteralPath $Path -Raw

        $Tables = ConvertFrom-HtmlTable -Content $Content -Engine AgilityPack -IncludeMetadata -AllProperties -CleanHeaders -EmptyValuePlaceholder '--'
        $DataTables = $Tables | Where-Object { $_.RowCount -gt 1 }

        $DataTables.Count | Should -BeGreaterOrEqual 7
        foreach ($table in $DataTables | Select-Object -First 7) {
            $table.Data.Count | Should -BeGreaterOrEqual 2
            ($table.Data[0].PSObject.Properties | Measure-Object).Count | Should -Be $table.ColumnCount
            ($table.Data[1].PSObject.Properties | Measure-Object).Count | Should -Be $table.ColumnCount
        }

        $First = $DataTables[0]
        $First.Headers | Should -Contain 'NonRegional'
        $First.Headers | Should -Not -Contain '*Non-Regional'
        $First.Data[0].NonRegional | Should -Be '--'
    }

    It 'Parses azure_status.html with AngleSharp and metadata' {
        $Path = Join-Path $PSScriptRoot 'Documents/azure_status.html'
        $Content = Get-Content -LiteralPath $Path -Raw

        $Tables = ConvertFrom-HtmlTable -Content $Content -Engine AngleSharp -IncludeMetadata -AllProperties -CleanHeaders -EmptyValuePlaceholder '--'
        $DataTables = $Tables | Where-Object { $_.RowCount -gt 1 }

        $DataTables.Count | Should -BeGreaterOrEqual 7
        foreach ($table in $DataTables | Select-Object -First 7) {
            $table.Data.Count | Should -BeGreaterOrEqual 2
            ($table.Data[0].PSObject.Properties | Measure-Object).Count | Should -Be $table.ColumnCount
            ($table.Data[1].PSObject.Properties | Measure-Object).Count | Should -Be $table.ColumnCount
        }

        $First = $DataTables[0]
        $First.Headers | Should -Contain 'NonRegional'
        $First.Headers | Should -Not -Contain '*Non-Regional'
        $First.Data[0].NonRegional | Should -Be '--'
    }
}
