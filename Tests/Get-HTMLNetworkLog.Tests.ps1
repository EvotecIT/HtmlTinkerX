Describe 'Get-HTMLNetworkLog' {
    It 'Captures network traffic from a session' {
        $path = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Start-Sleep -Milliseconds 500
        $log = Get-HTMLNetworkLog -Session $session
        Close-HTMLSession -Session $session

        $log.Count | Should -BeGreaterThan 0
    }
}
