Describe -Name 'ConvertFrom-HtmlTable' {
    It 'Given a HTML table online with polish chars - Should convert it to a PowerShell object' {
        $Url = 'https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm'

        $AllTables = ConvertFrom-HtmlTable -Url $Url -Engine AgilityPack
        $AllTables.Count | Should -Be 47
    }
    It 'Given a HTML Page with Tables' {
        $AllTables = ConvertFrom-HtmlTable -Url 'https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference'
        # There are 9 tables
        $AllTables.Count | Should -Be 9
    }

    It 'ConvertFrom-HTML cmdlet parses example.com' {
        $doc = ConvertFrom-HTML -Url 'https://example.com'
        $doc | Should -Not -BeNullOrEmpty
    }
}
