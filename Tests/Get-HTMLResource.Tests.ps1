Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Get-HTMLResource' {
    It 'Returns HtmlResourceLink objects with comments' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_resources.html'
        $links = Get-HTMLResource -Path $path -IncludeCss
        $links.Count | Should -Be 2
        $links[0] | Should -BeOfType PSParseHTML.HtmlResourceLink
        $links[0].Comment | Should -Be 'jQuery library'
        $links[0].Name | Should -Be 'sample.js'
        $links[1].Comment | Should -Be 'custom styles'
        $links[1].Name | Should -Be 'sample.css'
    }

    It 'Can return content of external resources' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_resources.html'
        $links = Get-HTMLResource -Path $path -IncludeCss -AsContent
        $links[0].Content | Should -Match 'sample script'
        $links[1].Content | Should -Match 'sample css'
    }

    It 'SaveAsync writes file to directory' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_resources.html'
        $links = Get-HTMLResource -Path $path -IncludeCss
        $dir = Join-Path $TestDrive 'out'
        $base = [System.IO.Path]::GetFullPath((Split-Path $path -Parent)) + [IO.Path]::DirectorySeparatorChar
        foreach ($link in $links) {
            $saved = $link.SaveAsync($dir, [Uri]::new($base)).GetAwaiter().GetResult()
            (Test-Path $saved) | Should -BeTrue
        }
    }
}
