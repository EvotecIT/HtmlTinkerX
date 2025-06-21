Describe 'Show-HTMLHar' {
    It 'Creates an HTML viewer from HAR' {
        $har = Join-Path $PSScriptRoot 'Documents/sample.har'
        $out = Join-Path $TestDrive 'viewer.html'
        $result = Show-HTMLHar -Path $har -OutFile $out
        (Test-Path $out) | Should -BeTrue
        $result.Log.entries[0].request.url | Should -Be 'https://example.com'
    }
}
