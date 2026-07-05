Describe 'Get-HTMLNetworkLog' {
    It 'Captures network traffic from a session' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            Complete-HtmlRoute -Route $route -Options @{
                Status = 200
                ContentType = 'application/json'
                Body = '{"message":"mock"}'
            }
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        Start-Sleep -Milliseconds 500

        $log = Get-HTMLNetworkLog -Session $session
        Close-HtmlBrowserSession -Session $session

        $log.Count | Should -BeGreaterThan 0
    }
}
