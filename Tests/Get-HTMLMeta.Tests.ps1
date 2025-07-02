Describe 'Get-HTMLMeta' {
    It 'Parses meta tags from sample HTML' {
        $path = Join-Path $PSScriptRoot 'Documents/headless_table.html'
        $content = Get-Content -LiteralPath $path -Raw
        $meta = Get-HTMLMeta -Content $content
        $meta.Count | Should -Be 2
        $meta[0].Name | Should -Be 'Content-Type'
        $meta[1].Name | Should -Be 'viewport'
    }
}
