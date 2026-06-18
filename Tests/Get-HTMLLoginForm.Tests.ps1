Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Get-HtmlBrowserLoginForm' {
    It 'exposes reusable launch profile parameters for one-shot login-form discovery' {
        $parameters = (Get-Command Get-HtmlBrowserLoginForm).Parameters.Keys

        $parameters | Should -Contain 'ProfilePath'
        $parameters | Should -Contain 'Scenario'
        $parameters | Should -Contain 'UserDataDirectory'
        $parameters | Should -Contain 'StatePath'
        $parameters | Should -Contain 'BrowserChannel'
        $parameters | Should -Contain 'LoadState'
        $parameters | Should -Contain 'BlockResourceType'
        $parameters | Should -Contain 'BlockResourcePattern'
    }

    It 'rejects document resource blocking for one-shot login-form discovery' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'

        { Get-HtmlBrowserLoginForm -Path $path -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

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

    It 'detects a login form directly from a file with scenario launch defaults' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'

        $form = Get-HtmlBrowserLoginForm -Path $path -Scenario LoginProtected -LoadState DomContentLoaded

        $form.UsernameSelector | Should -Be "input[name='user']"
        $form.PasswordSelector | Should -Be "input[name='pass']"
    }
}
