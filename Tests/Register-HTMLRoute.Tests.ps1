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
            Complete-HtmlRoute -Route $route -Options @{
                Status = 200
                ContentType = 'application/json'
                Body = '{"message":"mock"}'
            }
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'mock'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a route from untyped handlers' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            Complete-HtmlRoute -Route $route -Status 200 -ContentType 'application/json' -Body '{"message":"helper"}'
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a route from typed PowerShell route handlers' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param([PSParseHTML.PowerShell.PowerShellHtmlRoute] $route)
            Complete-HtmlRoute -Route $route -Status 200 -ContentType 'application/json' -Body '{"message":"typed-helper"}'
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'typed-helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a typed PowerShell route when the helper receives the raw route' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param([PSParseHTML.PowerShell.PowerShellHtmlRoute] $route)
            Complete-HtmlRoute -Route $route.Route -Status 200 -ContentType 'application/json' -Body '{"message":"typed-raw-helper"}'
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'typed-raw-helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a route from PowerShell objects as JSON' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            Complete-HtmlRoute -Route $route -Json ([pscustomobject]@{
                message = 'json-helper'
                nested  = [pscustomobject]@{
                    ok = $true
                }
            })
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'json-helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a route from PowerShell objects as JSON through options' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            Complete-HtmlRoute -Route $route -Options @{
                Json = [pscustomobject]@{
                    message = 'options-json-helper'
                    nested  = [pscustomobject]@{
                        ok = $true
                    }
                }
            }
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'options-json-helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Completes a route from a response file path' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $responsePath = Join-Path $TestDrive 'route-response.json'
        Set-Content -LiteralPath $responsePath -Value '{"message":"path-helper"}'
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        $handler = {
            param($route)
            Complete-HtmlRoute -Route $route -Status 200 -ContentType 'application/json' -Path $responsePath
        }.GetNewClosure()
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock $handler
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'path-helper'
        Close-HtmlBrowserSession -Session $session
    }

    It 'Passes original Playwright route to untyped handlers' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Register-HTMLRoute -Session $session -Pattern '**/data.json' -ScriptBlock {
            param($route)
            $message = if ($route -is [Microsoft.Playwright.IRoute]) { 'untyped-route' } else { 'wrapper' }
            Complete-HtmlRoute -Route $route -Options @{
                Status = 200
                ContentType = 'application/json'
                Body = "{`"message`":`"$message`"}"
            }
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
            Complete-HtmlRoute -Route $route -Options @{
                Status = 200
                ContentType = 'application/json'
                Body = '{"message":"typed"}'
            }
        }
        Invoke-HTMLNavigation -Session $session -Url $uri
        $text = Get-HtmlBrowserContent -Session $session -Selector '#result' -AsText
        $text | Should -Be 'typed'
        Close-HtmlBrowserSession -Session $session
    }
}
