Describe 'New-HtmlBrowserCookie' {
    It 'Creates a cookie object with basic properties' {
        $cookie = New-HtmlBrowserCookie -Name 'a' -Value '1' -Domain 'example.com' -Path '/'
        $cookie.Name | Should -Be 'a'
        $cookie.Value | Should -Be '1'
        $cookie.Domain | Should -Be 'example.com'
        $cookie.Path | Should -Be '/'
    }

    It 'Sets optional flags when switches are used' {
        $cookie = New-HtmlBrowserCookie -Name 'b' -Value '2' -HttpOnly -Secure
        $cookie.HttpOnly | Should -Be $true
        $cookie.Secure | Should -Be $true
    }
}
