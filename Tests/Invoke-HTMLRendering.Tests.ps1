Describe 'Invoke-HTMLRendering' {
    It 'Loads dynamic content from a local file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri
        $html | Should -Match 'Dynamic Content'
    }

    It 'Loads content using Firefox engine' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -Browser Firefox
        $html | Should -Match 'Dynamic Content'
    }

    It 'Saves page downloads to the specified directory' {
        $path = Join-Path $PSScriptRoot 'Documents/download.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $dest = Join-Path $TestDrive 'dl'
        $result = Invoke-HTMLRendering -Url $uri -DownloadPath $dest
        Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
        $result.Downloads | Should -Contain (Join-Path $dest 'download.txt')
    }
}
