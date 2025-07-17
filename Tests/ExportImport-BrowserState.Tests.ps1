Describe 'Browser state persistence' {
    if (Get-Command Export-BrowserState -ErrorAction SilentlyContinue) {
        It 'Reuses cookies from exported state' {
            $url = 'about:blank'
            $state = Join-Path $TestDrive 'state.json'

        $cookie = New-HTMLCookie -Name 'persist' -Value 'sweet' -Domain 'example.com' -Path '/'

        $s1 = Invoke-HTMLRendering -Url $url -Session
        Set-HTMLCookie -Session $s1 -Cookie ([HtmlTinkerX.HtmlCookie[]]@($cookie))
        Export-BrowserState -Session $s1 -Path $state
        Close-HTMLSession -Session $s1

        $s2 = Import-BrowserState -Path $state -Url $url
        $cookies = Get-HTMLCookie -Session $s2
        Close-HTMLSession -Session $s2

            ($cookies | Where-Object Name -eq 'persist').Value | Should -Be 'sweet'
        }
    } else {
        It 'Reuses cookies from exported state' -Skip {}
    }
}
