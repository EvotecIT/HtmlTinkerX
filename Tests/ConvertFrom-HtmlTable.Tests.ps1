Describe -Name 'ConvertFrom-HtmlTable' {
    It 'Given a HTML table online with polish chars - Should convert it to a PowerShell object' {
        $Url = 'https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm'

        $AllTables = ConvertFrom-HtmlTable -Url $Url -Engine AgilityPack
        $AllTables.Count | Should -BeGreaterThan 0
    }

    It 'Parses local HTML file with Polish characters correctly - AgilityPack' {
        $Path = Join-Path $PSScriptRoot 'Documents/polish_table.html'
        $Content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8

        $Table = ConvertFrom-HtmlTable -Content $Content -Engine AgilityPack
        $Table.Count | Should -Be 2

        # Check that Polish characters are preserved correctly
        $Table[0].Column1 | Should -Be 'Komórka a1'
        $Table[0].Column2 | Should -Be 'Komórka a2'
        $Table[1].Column1 | Should -Be 'Komórka a3'
        $Table[1].Column2 | Should -Be 'Komórka a4'
    }

    It 'Parses local HTML file with Polish characters correctly - AngleSharp' {
        $Path = Join-Path $PSScriptRoot 'Documents/polish_table.html'
        $Content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8

        $Table = ConvertFrom-HtmlTable -Content $Content -Engine AngleSharp
        $Table.Count | Should -Be 2

        # Check that Polish characters are preserved correctly
        $Table[0].Column1 | Should -Be 'Komórka a1'
        $Table[0].Column2 | Should -Be 'Komórka a2'
        $Table[1].Column1 | Should -Be 'Komórka a3'
        $Table[1].Column2 | Should -Be 'Komórka a4'
    }

    It 'Parses URL with Polish characters correctly - AgilityPack' {
        $Url = 'https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm'

        $AllTables = ConvertFrom-HtmlTable -Url $Url -Engine AgilityPack
        $AllTables.Count | Should -BeGreaterThan 0

        # Find the specific table we're testing (should be table index 12)
        if ($AllTables.Count -gt 12) {
            $Table = $AllTables[12]
            $Table.Count | Should -BeGreaterOrEqual 2

            # Check that Polish characters are preserved correctly
            $Table[0].Column1 | Should -Be 'Komórka a1'
            $Table[0].Column2 | Should -Be 'Komórka a2'
            $Table[1].Column1 | Should -Be 'Komórka a3'
            $Table[1].Column2 | Should -Be 'Komórka a4'
        }
    }

    It 'Parses URL with Polish characters correctly - AngleSharp' {
        $Url = 'https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm'

        $AllTables = ConvertFrom-HtmlTable -Url $Url -Engine AngleSharp
        $AllTables.Count | Should -BeGreaterThan 0

        # Find the specific table we're testing (should be table index 12)
        if ($AllTables.Count -gt 12) {
            $Table = $AllTables[12]
            $Table.Count | Should -BeGreaterOrEqual 2

            # Check that Polish characters are preserved correctly
            $Table[0].Column1 | Should -Be 'Komórka a1'
            $Table[0].Column2 | Should -Be 'Komórka a2'
            $Table[1].Column1 | Should -Be 'Komórka a3'
            $Table[1].Column2 | Should -Be 'Komórka a4'
        }
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
    It 'Parses headless_table.html and detects four tables' {
        $Path = Join-Path $PSScriptRoot 'Documents/headless_table.html'
        $Content = Get-Content -LiteralPath $Path -Raw
        $Tables = ConvertFrom-HtmlTable -Content $Content -Engine AgilityPack -IncludeMetadata

        $Tables.Count | Should -Be 4

        $Tables[0].TableIndex | Should -Be 0
        $Tables[0].ColumnCount | Should -Be 3
        $Tables[0].Data[0].Column3 | Should -Be 'Data64-bit'

        $Tables[2].Data[0].Column1 | Should -Be 'Source'
        $Tables[2].Data[0].Column2 | Should -Be 'D:'

        $Tables[3].Data[1].Column3 | Should -Match 'PrepareCopying failed'
    }
}
