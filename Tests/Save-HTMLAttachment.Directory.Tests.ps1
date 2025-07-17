Describe 'Save-HTMLAttachment path creation' {
    It 'Creates destination directory when missing' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/download.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $dest = Join-Path $TestDrive 'missing' 'dl'
        [array] $files = Save-HTMLAttachment -Url $uri -Path $dest
        Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
        Test-Path $dest | Should -BeTrue
        $files | Should -Contain (Join-Path $dest 'download.txt')
    }

    It 'Creates destination directory when missing for session input' {
        $pagePath = Join-Path $PSScriptRoot 'Documents/download.html'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $dest = Join-Path $TestDrive 'missing' 'session'
        Invoke-HTMLRendering -Url $uri -Session |
            Save-HTMLAttachment -Path $dest
        Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
        Test-Path $dest | Should -BeTrue
    }
}
