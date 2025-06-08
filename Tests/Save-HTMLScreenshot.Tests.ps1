Describe 'Save-HTMLScreenshot' {
    It 'Creates a screenshot file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'shot.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile
        (Test-Path $outfile) | Should -BeTrue
    }
}
