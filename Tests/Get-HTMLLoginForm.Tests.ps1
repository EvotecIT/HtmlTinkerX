Describe 'Get-HTMLLoginForm' {
    It 'Detects form fields from session' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        $form = Get-HTMLLoginForm -Session $session
        $form.UsernameSelector | Should -Be "input[name='user']"
        $form.PasswordSelector | Should -Be "input[name='pass']"
        $form.SubmitSelector | Should -Be "button[type='submit']"
        $form.LoginUrl | Should -Be $uri
        Close-HTMLSession -Session $session
    }

    It 'Returns null when no form found' {
        $path = Join-Path $PSScriptRoot 'Documents/headless_table.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $form = Get-HTMLLoginForm -Url $uri
        $form | Should -Be $null
    }
}
