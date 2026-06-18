Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'ConvertTo-HtmlWebRequestSession' {
    It 'exports the web request session bridge command and alias' {
        (Get-Command ConvertTo-HtmlWebRequestSession).Name | Should -Be 'ConvertTo-HtmlWebRequestSession'
        (Get-Alias ConvertTo-HtmlWebSession).Definition | Should -Be 'ConvertTo-HtmlWebRequestSession'
    }

    It 'converts HtmlCookie objects into a PowerShell WebRequestSession' {
        $cookie = New-HtmlBrowserCookie -Name 'auth' -Value 'cookie-value' -Domain 'example.com' -Path '/'

        $webSession = ConvertTo-HtmlWebRequestSession -Cookie $cookie -UserAgent 'PSParseHTML-Test' -Header @{ 'X-Test' = '1' }

        $webSession.GetType().FullName | Should -Be 'Microsoft.PowerShell.Commands.WebRequestSession'
        $webSession.UserAgent | Should -Be 'PSParseHTML-Test'
        $webSession.Headers['X-Test'] | Should -Be '1'
        $webSession.Cookies.GetCookies([uri]'https://example.com/')['auth'].Value | Should -Be 'cookie-value'
    }

    It 'accepts HtmlCookie values from the pipeline and skips expired cookies by default' {
        $expired = [DateTimeOffset]::UtcNow.AddDays(-1).ToUnixTimeSeconds()
        $cookies = @(
            (New-HtmlBrowserCookie -Name 'current' -Value 'yes' -Domain 'example.com' -Path '/'),
            (New-HtmlBrowserCookie -Name 'expired' -Value 'no' -Domain 'example.com' -Path '/' -Expires $expired)
        )

        $webSession = $cookies | ConvertTo-HtmlWebRequestSession -Quiet

        $stored = $webSession.Cookies.GetCookies([uri]'https://example.com/')
        $stored['current'].Value | Should -Be 'yes'
        $stored['expired'] | Should -Be $null
    }

    It 'converts cookies from an active browser session' {
        $session = Invoke-HtmlRendering -Url 'about:blank' -Session
        try {
            $cookie = New-HtmlBrowserCookie -Name 'browserAuth' -Value 'from-browser' -Domain 'example.com' -Path '/'
            Set-HtmlBrowserCookie -Session $session -Cookie ([HtmlTinkerX.HtmlCookie[]]@($cookie))

            $webSession = ConvertTo-HtmlWebRequestSession -Session $session -Domain 'example.com'

            $webSession.Cookies.GetCookies([uri]'https://example.com/')['browserAuth'].Value | Should -Be 'from-browser'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
