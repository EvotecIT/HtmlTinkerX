Describe 'Invoke-HTMLRendering' {
    It 'Loads dynamic content from a local file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri
        $html | Should -Match 'Dynamic Content'
    }

    It 'Loads content using Firefox engine' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -Browser Firefox
        $html | Should -Match 'Dynamic Content'
    }

    It 'Applies custom browser context options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session -UserAgent 'MyAgent' -ViewportWidth 123 -ViewportHeight 77 -DeviceScaleFactor 1.5
        $ua = $session.Page.EvaluateAsync('navigator.userAgent',$null).GetAwaiter().GetResult()
        $w = [int]($session.Page.EvaluateAsync('window.innerWidth',$null).GetAwaiter().GetResult().ToString())
        $d = [double]($session.Page.EvaluateAsync('window.devicePixelRatio',$null).GetAwaiter().GetResult().ToString())
        Close-HTMLSession -Session $session
        $ua | Should -Be 'MyAgent'
        $w | Should -Be 123
        [double]$d | Should -Be 1.5
    }

    It 'Applies geolocation and timezone settings' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session -GeoLatitude 50.1 -GeoLongitude 19.9 -Timezone 'Europe/Warsaw'
        $lat = [double]($session.Page.EvaluateAsync('new Promise(r=>navigator.geolocation.getCurrentPosition(p=>r(p.coords.latitude)))',$null).GetAwaiter().GetResult())
        $tz = $session.Page.EvaluateAsync('Intl.DateTimeFormat().resolvedOptions().timeZone',$null).GetAwaiter().GetResult()
        Close-HTMLSession -Session $session
        [math]::Round($lat,1) | Should -Be 50.1
        $tz | Should -Be 'Europe/Warsaw'
    }

    It 'Supports proxy parameters' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -Proxy 'http://localhost:8080'
        $html | Should -Match 'Dynamic Content'
    }
}
