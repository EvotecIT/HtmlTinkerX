Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Save-HtmlBrowserScreenshot' {
    It 'exposes reusable launch profile parameters for one-shot captures' {
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'ProfilePath'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'Scenario'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'UserDataDirectory'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'StatePath'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'Proxy'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'ProxyCredential'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'LoadState'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'Timeout'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'BlockResourceType'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'BlockResourcePattern'
    }

    It 'rejects document resource blocking for one-shot screenshot navigation' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $outfile = Join-Path $TestDrive 'blocked-document.png'

        { Save-HtmlBrowserScreenshot -Path $path -OutFile $outfile -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'Creates a screenshot file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'shot.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Supports proxy parameters' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'proxy.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Selector '#loaded' -Proxy 'http://localhost:8080'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Creates a full page screenshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'full.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Full -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'applies scenario viewport defaults to direct screenshots' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $outfile = Join-Path $TestDrive 'audit-proof.png'

        Save-HtmlBrowserScreenshot -Path $path -OutFile $outfile -Scenario AuditProof -Selector '#loaded'

        Add-Type -AssemblyName System.Drawing
        $bitmap = [System.Drawing.Bitmap]::new($outfile)
        try {
            $bitmap.Width | Should -Be 1366
            $bitmap.Height | Should -Be 900
        } finally {
            $bitmap.Dispose()
        }
    }

    It 'Creates a clipped screenshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'clip.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -X 0 -Y 0 -Width 50 -Height 50 -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Captures a single element screenshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'element.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Selector '#loaded' -ElementSelector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Highlights elements and overlays text' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'highlight.png'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Selector '#loaded' -HighlightSelector '#loaded' -OverlayText 'demo'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Defaults to temp file when using -Open without OutFile' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $before = Get-ChildItem ([System.IO.Path]::GetTempPath()) -Filter '*.png' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $beforeTime = if ($before) { $before.LastWriteTime } else { [datetime]::MinValue }
        Save-HtmlBrowserScreenshot -Url $uri -Selector '#loaded' -Open
        $after = Get-ChildItem ([System.IO.Path]::GetTempPath()) -Filter '*.png' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $after.LastWriteTime | Should -BeGreaterThan $beforeTime
    }
    It 'Respects delay and format parameters' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'delay.jpg'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -Selector '#loaded' -Format Jpeg -Quality 50 -Delay 100
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Adds extension when OutFile has none' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $base = Join-Path $TestDrive 'noext'
        Save-HtmlBrowserScreenshot -Url $uri -OutFile $base -Selector '#loaded' -Format Jpeg
        (Test-Path "$base.jpg") | Should -BeTrue
    }

    It 'Validates clip parameter range' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'invalid.png'
        { Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -X -1 -Y 0 -Width 10 -Height 10 -Selector '#loaded' } | Should -Throw
        { Save-HtmlBrowserScreenshot -Url $uri -OutFile $outfile -X 0 -Y 0 -Width 0 -Height 10 -Selector '#loaded' } | Should -Throw
    }
}
