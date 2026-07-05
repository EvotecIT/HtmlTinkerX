Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser navigation' {
    It 'exports direct URL load-state control' {
        $command = Get-Command Invoke-HtmlBrowserNavigation

        $command.Parameters.Keys | Should -Contain 'LoadState'
        $command.Parameters['LoadState'].Aliases | Should -Contain 'WaitUntil'
        $command.Parameters.Keys | Should -Contain 'NavigationUrl'
        $command.Parameters['NavigationUrl'].Aliases | Should -Contain 'WaitForUrl'
        $command.Parameters['NavigationUrl'].Aliases | Should -Contain 'UrlPattern'
    }

    It 'navigates direct URLs with DomContentLoaded when network activity stays open' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/streaming.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = @'
<!doctype html>
<html>
<body>
<main id="status">dom-ready</main>
<script>
fetch('/api/never-ending');
</script>
</body>
</html>
'@
                }
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/api/never-ending' -ScriptBlock {
                param($route)
                # Leave the request pending to prove navigation does not require network idle.
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/streaming.html' -LoadState DomContentLoaded -Timeout 2000
            Wait-HtmlBrowserContent -Session $session -Selector '#status' -Text 'dom-ready' -Exact -Timeout 1000

            $session.Page.Url | Should -Be 'https://example.com/streaming.html'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'waits for DomContentLoaded after selector clicks when network activity stays open' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/click-start.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = '<!doctype html><button id="go" onclick="location.href=''/click-finish.html''">Continue</button>'
                }
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/click-finish.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = '<!doctype html><main id="status">selector-click-ready</main><script>fetch(''/api/selector-never-ending'');</script>'
                }
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/api/selector-never-ending' -ScriptBlock {
                param($route)
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/click-start.html' -LoadState DomContentLoaded -Timeout 2000
            Invoke-HtmlBrowserNavigation -Session $session -Selector '#go' -WaitForNavigation -LoadState DomContentLoaded -NavigationUrl '**/click-finish.html' -Timeout 2000
            Wait-HtmlBrowserContent -Session $session -Selector '#status' -Text 'selector-click-ready' -Exact -Timeout 1000

            $session.Page.Url | Should -Be 'https://example.com/click-finish.html'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'waits for selector click navigation when the page reloads to the same URL' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/reload.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = '<!doctype html><button id="reload" onclick="location.reload()">Reload</button><main id="status">same-url-ready</main>'
                }
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/reload.html' -LoadState DomContentLoaded -Timeout 2000
            Invoke-HtmlBrowserNavigation -Session $session -Selector '#reload' -WaitForNavigation -LoadState DomContentLoaded -Timeout 2000
            Wait-HtmlBrowserContent -Session $session -Selector '#status' -Text 'same-url-ready' -Exact -Timeout 1000

            $session.Page.Url | Should -Be 'https://example.com/reload.html'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'waits for DomContentLoaded after text clicks when network activity stays open' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/text-start.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = '<!doctype html><button onclick="location.href=''/text-finish.html''">Continue with SSO</button>'
                }
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/text-finish.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = '<!doctype html><main id="status">text-click-ready</main><script>fetch(''/api/text-never-ending'');</script>'
                }
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/api/text-never-ending' -ScriptBlock {
                param($route)
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/text-start.html' -LoadState DomContentLoaded -Timeout 2000
            Invoke-HtmlBrowserNavigation -Session $session -Text 'Continue with SSO' -Exact -WaitForNavigation -LoadState DomContentLoaded -NavigationUrl '**/text-finish.html' -Timeout 2000
            Wait-HtmlBrowserContent -Session $session -Selector '#status' -Text 'text-click-ready' -Exact -Timeout 1000

            $session.Page.Url | Should -Be 'https://example.com/text-finish.html'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
