Describe 'Save-HTMLDownload' {
    It 'Saves downloads triggered by the page' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/download.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $dest = Join-Path $TestDrive 'dl'
        $files = Save-HTMLDownload -Url $uri -Path $dest
        Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
        $files | Should -Contain (Join-Path $dest 'download.txt')
    }
}
