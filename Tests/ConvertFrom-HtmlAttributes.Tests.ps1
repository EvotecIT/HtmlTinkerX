Describe -Name 'ConvertFrom-HTMLAttributes' {
    It -Name 'Given a valid URL - Should return the content of the tag' {
        $Path = Join-Path $PSScriptRoot 'Documents/em_sample.html'
        $Html = Get-Content -LiteralPath $Path -Raw -Encoding UTF8

        $Content = ConvertFrom-HTMLAttributes -Content $Html -Tag 'em'
        $Content | Should -Be 'awesome'
        $Content = ConvertFrom-HTMLAttributes -Content $Html -Tag 'em'
        $Content | Should -Be 'awesome'
    }
}
