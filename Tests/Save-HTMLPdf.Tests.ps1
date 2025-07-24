Describe 'Save-HtmlBrowserPdf' {
    It 'Creates a PDF file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'page.pdf'
        Save-HtmlBrowserPdf -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Accepts session objects from the pipeline' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'pipe.pdf'
        Invoke-HTMLRendering -Url $uri -Session |
            Save-HtmlBrowserPdf -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }
    It 'Generates PDF with custom layout options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'layout.pdf'
        Save-HtmlBrowserPdf -Url $uri -OutFile $outfile -Selector '#loaded' -Landscape -PrintBackground -Format A4 -MarginTop "0.5in" -MarginBottom "0.5in"
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Creates a PDF file in a new directory' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $folder = Join-Path $TestDrive 'newdir'
        $outfile = Join-Path $folder 'nested.pdf'
        Save-HtmlBrowserPdf -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
        (Test-Path $folder) | Should -BeTrue
    }
}
