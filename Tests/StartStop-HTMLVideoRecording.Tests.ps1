describe 'HTML Video Recording' {
    it 'Records a short video' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'video.webm'
        $session = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Session $session -Url $uri
        # Ensure at least one rendered frame by waiting for dynamic content
        $null = $session.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $session
        (Test-Path $out) | Should -BeTrue
    }

    it 'Uses default session variable' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'default.webm'
        $null = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Url $uri
        $null = $PSParseHTML_DefaultSession.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture
        (Test-Path $out) | Should -BeTrue
    }

    it 'Starts recording from existing session' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        $out = Join-Path $TestDrive 'existing.webm'
        $record = Start-HtmlBrowserVideoCapture -Session $session -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Session $record -Url $uri
        $null = $record.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $record
        (Test-Path $out) | Should -BeTrue
    }

    it 'Applies custom options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'opts.webm'
        $session = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240 -UserAgent 'VideoUA' -ViewportWidth 200 -ViewportHeight 150 -DeviceScaleFactor 2
        $ua = $session.Page.EvaluateAsync('navigator.userAgent',$null).GetAwaiter().GetResult()
        $w = [int]($session.Page.EvaluateAsync('window.innerWidth',$null).GetAwaiter().GetResult().ToString())
        $d = [double]($session.Page.EvaluateAsync('window.devicePixelRatio',$null).GetAwaiter().GetResult().ToString())
        $null = $session.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $session
        $ua | Should -Be 'VideoUA'
        $w | Should -Be 200
        [double]$d | Should -Be 2
    }

    it 'Applies geolocation and timezone options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'geo.webm'
        $session = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240 -GeoLatitude 40.0 -GeoLongitude -74.0 -Timezone 'America/New_York'
        $lat = [double]($session.Page.EvaluateAsync('new Promise(r=>navigator.geolocation.getCurrentPosition(p=>r(p.coords.latitude)))',$null).GetAwaiter().GetResult().ToString())
        $tz = $session.Page.EvaluateAsync('Intl.DateTimeFormat().resolvedOptions().timeZone',$null).GetAwaiter().GetResult()
        $null = $session.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $session
        [math]::Round($lat,0) | Should -Be 40
        $tz | Should -Be 'America/New_York'
    }

    it 'Supports .WebM extension' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'caps.WebM'
        $session = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Session $session -Url $uri
        $null = $session.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $session -OutFile $out
        (Test-Path $out) | Should -BeTrue
    }

    it 'Supports .webm extension when stopping' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'lower.webm'
        $session = Start-HtmlBrowserVideoCapture -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Session $session -Url $uri
        $null = $session.Page.WaitForSelectorAsync('#loaded')
        Start-Sleep -Milliseconds 150
        Stop-HtmlBrowserVideoCapture -Session $session -OutFile $out
        (Test-Path $out) | Should -BeTrue
    }
}
