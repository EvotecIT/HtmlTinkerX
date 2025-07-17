Describe -Name 'ConvertFrom-HTMLTag' {
    It -Name 'Given a valid URL - Should return the content of the tag' {
        $Path = Join-Path $PSScriptRoot 'Documents/em_sample.html'
        $Html = Get-Content -LiteralPath $Path -Raw -Encoding UTF8

        $Content = ConvertFrom-HTMLTag -Content $Html -Tag 'em'
        $Content | Should -Be 'awesome'
        $Content = ConvertFrom-HTMLTag -Content $Html -Tag 'em'
        $Content | Should -Be 'awesome'
    }

    It 'ConvertFrom-HTML cmdlet works' {
        $Path = Join-Path $PSScriptRoot 'Documents/em_sample.html'
        $Html = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        $doc = ConvertFrom-HTML -Content $Html
        $doc | Should -Not -BeNullOrEmpty
    }
}
