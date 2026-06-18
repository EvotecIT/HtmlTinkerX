Describe 'Proxy parameters' {
    It 'Cmdlets expose proxy parameters' {
        $cmdlets = 'ConvertFrom-HTML','ConvertFrom-HtmlTable','ConvertFrom-HtmlAttributes','ConvertFrom-HtmlList','Convert-HTMLToText','Invoke-HTMLRendering','Save-HtmlBrowserScreenshot','Get-HtmlBrowserInteractable','Get-HtmlBrowserLoginForm','Get-HtmlBrowserSsoHandoff'
        foreach($cmd in $cmdlets){
            $params = (Get-Command $cmd).Parameters.Keys
            $params | Should -Contain 'Proxy'
            $params | Should -Contain 'ProxyCredential'
        }
    }

    It 'Throws when ProxyCredential used without Proxy' {
        $cred = New-Object PSCredential('u',(ConvertTo-SecureString 'p' -AsPlainText -Force))
        { Invoke-HTMLRendering -Url 'http://example.com' -ProxyCredential $cred } | Should -Throw
        { Get-HtmlBrowserInteractable -Url 'http://example.com' -ProxyCredential $cred } | Should -Throw
        { Get-HtmlBrowserLoginForm -Url 'http://example.com' -ProxyCredential $cred } | Should -Throw
        { Get-HtmlBrowserSsoHandoff -Url 'http://example.com' -ProxyCredential $cred } | Should -Throw
        { Set-HtmlBrowserClientOption -ProxyCredential $cred } | Should -Throw
    }
}
