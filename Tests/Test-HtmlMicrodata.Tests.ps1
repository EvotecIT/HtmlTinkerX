Describe 'Test-HtmlMicrodata' {
    It 'Detects unknown properties from content' {
        $path = Join-Path $PSScriptRoot 'Documents/microdata_mismatch.html'
        $html = Get-Content -LiteralPath $path -Raw
        $mismatches = Test-HtmlMicrodata -Content $html
        $mismatches.Count | Should -Be 1
        $mismatches[0].Properties | Should -Contain 'age'
    }

    It 'Works with pipeline input' {
        $path = Join-Path $PSScriptRoot 'Documents/microdata_mismatch.html'
        $html = Get-Content -LiteralPath $path -Raw
        $items = ConvertFrom-HtmlMicrodata -Content $html
        $result = $items | Test-HtmlMicrodata
        $result.Count | Should -Be 1
        $result[0].Type | Should -Be 'https://schema.org/Person'
    }
}
