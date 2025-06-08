# Describe 'Save-HTMLDownload' {
#     It 'Saves downloads triggered by the page' {
#         $pagePath = Join-Path $PSScriptRoot 'Documents/download.html'
#         $uri = [System.Uri]::new($pagePath).AbsoluteUri
#         $dest = Join-Path $TestDrive 'dl'
#         $files = Save-HTMLDownload -Url $uri -Path $dest
#         Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
#         $files | Should -Contain (Join-Path $dest 'download.txt')
#     }

#     It 'Downloads links when no filter is specified' {
#         $pagePath = Join-Path $PSScriptRoot 'Documents/manual_download.html'
#         $uri = [System.Uri]::new($pagePath).AbsoluteUri
#         $dest = Join-Path $TestDrive 'all'
#         $files = Save-HTMLDownload -Url $uri -Path $dest
#         Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
#         $files | Should -Contain (Join-Path $dest 'download.txt')
#     }

#     It 'Downloads links when filtered' {
#         $pagePath = Join-Path $PSScriptRoot 'Documents/manual_download.html'
#         $uri = [System.Uri]::new($pagePath).AbsoluteUri
#         $dest = Join-Path $TestDrive 'filtered'
#         $files = Save-HTMLDownload -Url $uri -Path $dest -Filter 'download.txt'
#         Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
#         $files | Should -Contain (Join-Path $dest 'download.txt')
#     }
# }
