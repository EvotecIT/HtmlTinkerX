Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Get-HtmlCrawlProfile' {
    It 'Lists built-in crawl profiles' {
        $profiles = Get-HtmlCrawlProfile

        $profiles.Name | Should -Contain 'evotec-xyz'
        $profiles.Name | Should -Contain 'wordpress-content'
    }

    It 'Can filter built-in crawl profiles by name' {
        $profile = Get-HtmlCrawlProfile -Name 'evotec-xyz'

        @($profile).Count | Should -Be 1
        $profile.Name | Should -Be 'evotec-xyz'
    }

    It 'Can read custom crawl profiles from JSON' {
        $profilePath = Join-Path $TestDrive 'crawl-profiles-get.json'
        @'
[
  {
    "name": "custom-docs",
    "hosts": [ "docs.example.com" ],
    "selector": "article",
    "contentMode": "Reader",
    "readerMinimumWordCount": 30,
    "readerMinimumScore": 40
  }
]
'@ | Set-Content -Path $profilePath

        $profile = Get-HtmlCrawlProfile -Path $profilePath -Name 'custom-docs'

        @($profile).Count | Should -Be 1
        $profile.Name | Should -Be 'custom-docs'
        $profile.Hosts | Should -Contain 'docs.example.com'
        $profile.Selector | Should -Be 'article'
        $profile.ContentMode | Should -Be 'Reader'
        $profile.ReaderMinimumWordCount | Should -Be 30
        $profile.ReaderMinimumScore | Should -Be 40
    }
}
