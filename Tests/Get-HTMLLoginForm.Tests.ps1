Describe 'Get-HtmlBrowserLoginForm' {
    It 'Detects login form from a session' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        $form = Get-HtmlBrowserLoginForm -Session $session
        Close-HtmlBrowserSession -Session $session

        $form.LoginUrl | Should -Be $uri
        $form.UsernameSelector | Should -Be "input[name='user']"
        $form.PasswordSelector | Should -Be "input[name='pass']"
        $form.SubmitSelector | Should -Be 'button'
    }
}
