Describe 'ConvertFrom-HTMLCookie' {
    It 'Parses Set-Cookie headers' {
        $header = 'Set-Cookie: id=abc; Path=/; Secure'
        $cookie = ConvertFrom-HTMLCookie -Content $header -Format SetCookie
        $cookie.Name | Should -Be 'id'
        $cookie.Value | Should -Be 'abc'
        $cookie.Secure | Should -Be $true
        $cookie.Path | Should -Be '/'
    }

    It 'Parses Netscape cookie lines' {
        $line = "example.com`tFALSE`t/`tFALSE`t0`tID`tabc"
        $cookies = ConvertFrom-HTMLCookie -Content $line -Format Netscape
        $cookies.Count | Should -Be 1
        $cookies[0].Name | Should -Be 'ID'
    }

    It 'Works with Set-HTMLCookie' {
        $line = "example.com`tFALSE`t/`tFALSE`t0`tID`tabc"
        $cookies = ConvertFrom-HTMLCookie -Content $line -Format Netscape
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        Set-HTMLCookie -Session $session -Cookie $cookies
        $result = Get-HTMLCookie -Session $session -Domain 'example.com'
        Close-HTMLSession -Session $session
        $result.Count | Should -Be 1
        $result[0].Name | Should -Be 'ID'
    }
}
