Describe 'Save-HTMLScreenshot' {
    It 'Creates a screenshot file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'shot.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Creates a full page screenshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'full.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile -Full -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Creates a clipped screenshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'clip.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile -X 0 -Y 0 -Width 50 -Height 50 -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }
}
