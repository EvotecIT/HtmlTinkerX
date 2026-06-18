Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Save-HtmlBrowserPdf' {
    It 'exposes reusable launch profile parameters for one-shot captures' {
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'ProfilePath'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'Scenario'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'UserDataDirectory'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'StatePath'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'Proxy'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'ProxyCredential'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'LoadState'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'Timeout'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'BlockResourceType'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'BlockResourcePattern'
    }

    It 'rejects document resource blocking for one-shot PDF navigation' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $outfile = Join-Path $TestDrive 'blocked-document.pdf'

        { Save-HtmlBrowserPdf -Path $path -OutFile $outfile -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

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

    It 'accepts scenario launch defaults for direct PDF captures' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $outfile = Join-Path $TestDrive 'scenario.pdf'
        Save-HtmlBrowserPdf -Path $path -OutFile $outfile -Selector '#loaded' -Scenario AuditProof
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Accepts visual mask options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'masked.pdf'
        Save-HtmlBrowserPdf -Url $uri -OutFile $outfile -Selector '#loaded' -MaskSensitiveElement -MaskSelector '#loaded' -MaskColor '#00ff00'
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
