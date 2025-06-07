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
