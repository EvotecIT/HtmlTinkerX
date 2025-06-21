Describe 'HTML Cookie cmdlets' {
    It 'Persists cookies between sessions' {
        $url = 'about:blank'

        $cookie = [PSParseHTML.HtmlCookie]::new()
        $cookie.Name = 'mycookie'
        $cookie.Value = 'choco'
        $cookie.Domain = 'example.com'
        $cookie.Path = '/'

        $session1 = Invoke-HTMLRendering -Url $url -Session
        Set-HTMLCookie -Session $session1 -Cookie ([PSParseHTML.HtmlCookie[]]@($cookie))
        $cookies1 = Get-HTMLCookie -Session $session1
        Close-HTMLSession -Session $session1

        ($cookies1 | Where-Object Name -eq 'mycookie').Value | Should -Be 'choco'

        $session2 = Invoke-HTMLRendering -Url $url -Session
        $cookies2 = Get-HTMLCookie -Session $session2
        ($cookies2 | Where-Object Name -eq 'mycookie') | Should -Be $null

        Set-HTMLCookie -Session $session2 -Cookie $cookies1
        $after = Get-HTMLCookie -Session $session2
        Close-HTMLSession -Session $session2

        ($after | Where-Object Name -eq 'mycookie').Value | Should -Be 'choco'
    }

    It 'Accepts an empty cookie list' {
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        { Set-HTMLCookie -Session $session -Cookie @() } | Should -Not -Throw
        Close-HTMLSession -Session $session
    }
}
