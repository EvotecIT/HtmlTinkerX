Describe 'Save-HTMLScreenshot' {
    It 'Creates a screenshot file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'shot.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile -Selector '#loaded'
        (Test-Path $outfile) | Should -BeTrue
    }

    It 'Supports proxy parameters' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $outfile = Join-Path $TestDrive 'proxy.png'
        Save-HTMLScreenshot -Url $uri -OutFile $outfile -Selector '#loaded' -Proxy 'http://localhost:8080'
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

    It 'Defaults to temp file when using -Open without OutFile' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $before = Get-ChildItem ([System.IO.Path]::GetTempPath()) -Filter '*.png' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $beforeTime = if ($before) { $before.LastWriteTime } else { [datetime]::MinValue }
        Save-HTMLScreenshot -Url $uri -Selector '#loaded' -Open
        $after = Get-ChildItem ([System.IO.Path]::GetTempPath()) -Filter '*.png' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $after.LastWriteTime | Should -BeGreaterThan $beforeTime
    }
}
