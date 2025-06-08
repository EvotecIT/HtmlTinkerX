Describe 'Save-HTMLDownload' {
    It 'Saves downloads on the page by filter' {
        $Dest = Join-Path $TestDrive 'dl\DnsClientX-PowerShellModule.v0.4.0.zip'
        [Array] $File = Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$Dest" -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
        $File.Count | Should -Be 2
        $Item1 = Get-Item -Path $File[0]
        $Item2 = Get-Item -Path $File[1]

        $Item1.Name | Should -Be 'DnsClientX-PowerShellModule.v0.4.0.zip'
        $Item2.Name | Should -Be 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
    }
}
