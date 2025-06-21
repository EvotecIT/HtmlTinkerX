Describe 'Browser runtime caching' {
    It 'Installs the runtime only once' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $first = Invoke-HTMLRendering -Url $uri 2>&1
        $second = Invoke-HTMLRendering -Url $uri 2>&1
        ($first | Select-String 'Downloading').Count | Should -BeGreaterThan 0
        ($second | Select-String 'Downloading').Count | Should -Be 0
    }
}
