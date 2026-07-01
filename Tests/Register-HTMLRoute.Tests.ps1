Describe 'Register-HTMLRoute' {
    It 'Blocks a request to data.json' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock { param($route) $route.AbortAsync() | Out-Null }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'error'
        Close-HtmlBrowserSession -Session $session
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
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'mock'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Passes original Playwright route to untyped handlers' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            $message = if ($route -is [Microsoft.Playwright.IRoute]) { 'untyped-route' } else { 'wrapper' }
            $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                Status = 200
                ContentType = 'application/json'
                Body = "{`"message`":`"$message`"}"
            }) | Out-Null
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'untyped-route'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Passes original Playwright route to typed handlers' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param([Microsoft.Playwright.IRoute] $route)
            $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                Status = 200
                ContentType = 'application/json'
                Body = '{"message":"typed"}'
            }) | Out-Null
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'typed'
        Close-HtmlBrowserSession -Session $session
    }
}
