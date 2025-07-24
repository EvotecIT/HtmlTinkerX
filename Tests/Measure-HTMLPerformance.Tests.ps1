Describe 'Measure-HtmlBrowserPerformance' {
    It 'Returns performance metrics' {
        $metrics = Measure-HtmlBrowserPerformance -Url 'https://example.com'

        $metrics | Should -Not -BeNullOrEmpty
        $metrics.TotalRequests | Should -BeGreaterOrEqual 0
        $metrics.TotalLoadTime | Should -Not -BeNullOrEmpty
    }
}
