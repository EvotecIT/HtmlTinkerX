Describe 'HTML Cookie cmdlets' {
    It 'Persists cookies between sessions' {
        $url = 'about:blank'

        $cookie = New-HtmlBrowserCookie -Name 'mycookie' -Value 'choco' -Domain 'example.com' -Path '/'

        $session1 = Invoke-HTMLRendering -Url $url -Session
        Set-HtmlBrowserCookie -Session $session1 -Cookie ([HtmlTinkerX.HtmlCookie[]]@($cookie))
        $cookies1 = Get-HtmlBrowserCookie -Session $session1
        Close-HtmlBrowserSession -Session $session1

        ($cookies1 | Where-Object Name -eq 'mycookie').Value | Should -Be 'choco'

        $session2 = Invoke-HTMLRendering -Url $url -Session
        $cookies2 = Get-HtmlBrowserCookie -Session $session2
        ($cookies2 | Where-Object Name -eq 'mycookie') | Should -Be $null

        Set-HtmlBrowserCookie -Session $session2 -Cookie $cookies1
        $after = Get-HtmlBrowserCookie -Session $session2
        Close-HtmlBrowserSession -Session $session2

        ($after | Where-Object Name -eq 'mycookie').Value | Should -Be 'choco'
    }

    It 'Accepts an empty cookie list' {
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        { Set-HtmlBrowserCookie -Session $session -Cookie @() } | Should -Not -Throw
        Close-HtmlBrowserSession -Session $session
    }

    It 'Filters cookies by domain' {
        $url = 'about:blank'

        $c1 = New-HtmlBrowserCookie -Name 'c1' -Value 'val1' -Domain 'example.com' -Path '/'

        $c2 = New-HtmlBrowserCookie -Name 'c2' -Value 'val2' -Domain 'other.com' -Path '/'

        $session = Invoke-HTMLRendering -Url $url -Session
        Set-HtmlBrowserCookie -Session $session -Cookie ([HtmlTinkerX.HtmlCookie[]]@($c1, $c2))
        $filtered = Get-HtmlBrowserCookie -Session $session -Domain 'example.com'
        Close-HtmlBrowserSession -Session $session

        $filtered.Count | Should -Be 1
        $filtered[0].Name | Should -Be 'c1'
    }
}
