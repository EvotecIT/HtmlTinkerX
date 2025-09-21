Describe 'Measure-HtmlBrowserPerformance' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot 'Common/TestHelpers.psm1') -Force
        $script:Site = Start-TestSite -Root $PSScriptRoot
    }
    AfterAll { if ($script:Site) { $script:Site | Stop-TestSite } }

    It 'Returns performance metrics' {
        $url = Get-TestUrl -Site $Site -RelativePath 'Documents/sample_resources.html'
        $metrics = Measure-HtmlBrowserPerformance -Url $url

        $metrics | Should -Not -BeNullOrEmpty
        $metrics.TotalRequests | Should -BeGreaterOrEqual 0
        $metrics.TotalLoadTime | Should -Not -BeNullOrEmpty
    }
}
