Describe 'HTML Cookie cmdlets' {
    It 'Persists cookies between sessions' {
        $url = 'about:blank'

        $cookie = New-HTMLCookie -Name 'mycookie' -Value 'choco' -Domain 'example.com' -Path '/'

        $session1 = Invoke-HTMLRendering -Url $url -Session
        Set-HTMLCookie -Session $session1 -Cookie ([HtmlTinkerX.HtmlCookie[]]@($cookie))
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

    It 'Filters cookies by domain' {
        $url = 'about:blank'

        $c1 = New-HTMLCookie -Name 'c1' -Value 'val1' -Domain 'example.com' -Path '/'

        $c2 = New-HTMLCookie -Name 'c2' -Value 'val2' -Domain 'other.com' -Path '/'

        $session = Invoke-HTMLRendering -Url $url -Session
        Set-HTMLCookie -Session $session -Cookie ([HtmlTinkerX.HtmlCookie[]]@($c1, $c2))
        $filtered = Get-HTMLCookie -Session $session -Domain 'example.com'
        Close-HTMLSession -Session $session

        $filtered.Count | Should -Be 1
        $filtered[0].Name | Should -Be 'c1'
    }
}
