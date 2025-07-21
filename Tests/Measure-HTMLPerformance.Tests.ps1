Describe 'Measure-HTMLPerformance' {
    It 'Returns performance metrics' {
        $metrics = Measure-HTMLPerformance -Url 'https://example.com'

        $metrics | Should -Not -BeNullOrEmpty
        $metrics.TotalRequests | Should -BeGreaterOrEqual 0
        $metrics.TotalLoadTime | Should -Not -BeNullOrEmpty
    }
}
