Describe 'Save-HTMLAttachment' {
    It 'Saves downloads on the page by filter' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        Import-Module (Join-Path $PSScriptRoot 'Common/TestHelpers.psm1') -Force
        # Arrange a local site with two zip assets
        $root = Join-Path $TestDrive 'site'
        New-Item -ItemType Directory -Path $root | Out-Null
        $c1 = Join-Path $root 'c1'; New-Item -ItemType Directory -Path $c1 | Out-Null
        $c2 = Join-Path $root 'c2'; New-Item -ItemType Directory -Path $c2 | Out-Null
        Set-Content -Path (Join-Path $c1 'a.txt') -Value 'one'
        Set-Content -Path (Join-Path $c2 'b.txt') -Value 'two'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip1 = Join-Path $root 'DnsClientX-PowerShellModule.v0.4.0.zip'
        $zip2 = Join-Path $root 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
        [IO.Compression.ZipFile]::CreateFromDirectory($c1, $zip1)
        [IO.Compression.ZipFile]::CreateFromDirectory($c2, $zip2)
        $html = @"
<!DOCTYPE html>
<html><body>
  <a href="DnsClientX-PowerShellModule.v0.4.0.zip">asset1</a>
  <a href="DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip">asset2</a>
  <script>document.addEventListener('DOMContentLoaded',()=>window.scrollTo(0,document.body.scrollHeight));</script>
  </body></html>
"@
        Set-Content -Encoding utf8 -Path (Join-Path $root 'links.html') -Value $html

        # Start a local server bound to IPv4 on a free port
        $site = Initialize-TestSite -Root $root
        try {
            # Act
            $dest = Join-Path $TestDrive 'dl'
            [Array] $files = Save-HTMLAttachment -Url (Get-TestUrl -Site $site -RelativePath 'links.html') -Path $dest -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'

            # Assert
            $files.Count | Should -Be 2
            $names = $files | ForEach-Object { (Get-Item $_).Name }
            $names | Should -Contain 'DnsClientX-PowerShellModule.v0.4.0.zip'
            $names | Should -Contain 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
        } finally {
            $site | Cleanup-TestSite
        }
    }

    It 'Downloads are fully written to disk' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        Import-Module (Join-Path $PSScriptRoot 'Common/TestHelpers.psm1') -Force
        # Reuse the same flow, verifying sizes > 0
        $root = Join-Path $TestDrive 'site2'
        New-Item -ItemType Directory -Path $root | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        1..2 | ForEach-Object {
            $dir = Join-Path $root "c$_"; New-Item -ItemType Directory -Path $dir | Out-Null; Set-Content -Path (Join-Path $dir "f$_`n.txt") -Value "data$_"
        }
        [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'c1'), (Join-Path $root 'DnsClientX-PowerShellModule.v0.4.0.zip'))
        [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'c2'), (Join-Path $root 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'))
        Set-Content -Encoding utf8 -Path (Join-Path $root 'links.html') -Value '<a href="DnsClientX-PowerShellModule.v0.4.0.zip">a</a><a href="DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip">b</a>'
        $site = Initialize-TestSite -Root $root
        try {
            $dest = Join-Path $TestDrive 'dl-full'
            [array] $files = Save-HTMLAttachment -Url (Get-TestUrl -Site $site -RelativePath 'links.html') -Path $dest -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
            foreach ($p in $files) {
                (Get-Item $p).Length | Should -BeGreaterThan 0
            }
        } finally {
            $site | Cleanup-TestSite
        }
    }
}
