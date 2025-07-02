Describe 'ConvertFrom-HtmlMicrodata' {
    It 'Parses microdata items from content' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_microdata.html'
        $content = Get-Content -LiteralPath $path -Raw
        $items = ConvertFrom-HtmlMicrodata -Content $content
        $items.Count | Should -Be 1
        $items[0].Properties.name[0] | Should -Be 'Jane Doe'
        $items[0].Type | Should -Be 'https://schema.org/Person'
    }
}
