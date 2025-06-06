Describe 'Optimize-Email' {    It 'Given HTML content - Should inline CSS and remove style elements' {
        $html = '<html><head><style>p{color:red}</style></head><body><p>Hi</p></body></html>'
        $expected = '<html><head></head><body><p style="color: red">Hi</p></body></html>'
        $result = Optimize-Email -Body $html -RemoveStyleElements
        # Normalize line endings to handle cross-platform differences
        $normalizedResult = $result -replace '\r\n', "`n" -replace '\r', "`n"
        $normalizedExpected = $expected -replace '\r\n', "`n" -replace '\r', "`n"
        $normalizedResult | Should -Be $normalizedExpected
    }

    It 'Given HTML with media query - Should preserve media queries when requested' {
        $html = '<html><head><style>p{color:red}@media(max-width:600px){p{font-size:14px;}}</style></head><body><p>Hi</p></body></html>'
        $result = Optimize-Email -Body $html -RemoveStyleElements -PreserveMediaQueries
        Should -Actual $result -Match '<style>@media'
        Should -Not -Actual $result -Match 'p{color:red}'
    }
    It 'Given file input - Should process HTML file' {
        $file = Join-Path $TestDrive 'email.html'
        '<html><head><style>p{color:red}</style></head><body><p>File</p></body></html>' | Set-Content -Path $file
        $expected = @"
<html><head></head><body><p style="color: red">File</p>
</body></html>
"@
        $result = Optimize-Email -Path $file -RemoveStyleElements
        # Normalize line endings to handle cross-platform differences
        $normalizedResult = $result -replace '\r\n', "`n" -replace '\r', "`n"
        $normalizedExpected = $expected -replace '\r\n', "`n" -replace '\r', "`n"
        Should -Actual $normalizedResult -Be $normalizedExpected
    }
}
