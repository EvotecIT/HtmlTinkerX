Describe 'Credential parameters' {
    It 'Invoke-HTMLRendering exposes credential parameters' {
        $params = (Get-Command Invoke-HTMLRendering).Parameters.Keys
        $params | Should -Contain 'Credential'
        $params | Should -Contain 'Username'
        $params | Should -Contain 'Password'
        $params | Should -Contain 'LoginUrl'
        $params | Should -Contain 'UsernameSelector'
        $params | Should -Contain 'PasswordSelector'
        $params | Should -Contain 'SubmitSelector'
    }
}
