Describe 'Register-HTMLRoute' {
    It 'Blocks a request to data.json' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock { param($route) $route.AbortAsync() | Out-Null }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HTMLContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'error'
        Close-HTMLSession -Session $session
    }

    It 'Rewrites request to data.json' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                Status = 200
                ContentType = 'application/json'
                Body = '{"message":"mock"}'
            }) | Out-Null
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HTMLContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'mock'
        Close-HTMLSession -Session $session
    }
}
