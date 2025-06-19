Describe 'Save-HTMLPdf' {
    It 'Creates a PDF file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'page.pdf'
        Save-HTMLPdf -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Accepts session objects from the pipeline' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'pipe.pdf'
        Invoke-HTMLRendering -Url $uri -Session |
            Save-HTMLPdf -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }
}
