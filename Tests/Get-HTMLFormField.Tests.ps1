Describe 'Get-HTMLFormField' {
    It 'Returns fields from sample HTML' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $content = Get-Content -LiteralPath $path -Raw
        $fields = Get-HTMLFormField -Content $content
        $fields.Count | Should -Be 3
        $fields[0].Name | Should -Be 'user'
        $fields[0].Type | Should -Be 'text'
    }
}
