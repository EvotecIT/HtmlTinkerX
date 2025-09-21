Describe 'Save-HTMLAttachment streaming' {
    It 'Outputs file paths as downloads complete' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        Import-Module (Join-Path $PSScriptRoot 'Common/TestHelpers.psm1') -Force
        $site = Start-TestSite -Root (Join-Path $PSScriptRoot 'Documents')
        try {
            $uri = Get-TestUrl -Site $site -RelativePath 'multi_download.html'
            $dest = Join-Path $TestDrive 'stream'
            $results = @()
            foreach ($file in Save-HTMLAttachment -Url $uri -Path $dest) {
                $results += $file
            }
            $results.Count | Should -Be 2
            Test-Path (Join-Path $dest 'download1.txt') | Should -BeTrue
            Test-Path (Join-Path $dest 'download2.txt') | Should -BeTrue
        }
        finally {
            $site | Stop-TestSite
        }
    }
}
