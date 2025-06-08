Describe 'Get-RenderedHtml' {
    It 'Loads dynamic content from a local file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Get-RenderedHtml -Url $uri
        $html | Should -Match 'Dynamic Content'
    }
}
