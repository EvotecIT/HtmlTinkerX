Describe 'ConvertFrom-HtmlForm' {
    It 'Parses sample forms using AngleSharp' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $content = Get-Content -LiteralPath $path -Raw
        $forms = ConvertFrom-HtmlForm -Content $content
        $forms.Count | Should -Be 2
        $forms[0].Action | Should -Be '/login'
        $forms[0].Method | Should -Be 'POST'
        $forms[0].Fields[0].Name | Should -Be 'user'
        $forms[0].Fields[1].Type | Should -Be 'password'
    }
}
