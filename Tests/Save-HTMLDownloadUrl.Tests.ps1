Describe 'Save-HTMLAttachment' {
    It 'Saves downloads on the page by filter' {
        $Dest = Join-Path $TestDrive 'dl\DnsClientX-PowerShellModule.v0.4.0.zip'
        [Array] $File = Save-HTMLAttachment -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$Dest" -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
        $File.Count | Should -BeGreaterOrEqual 2
        $Item1 = Get-Item -Path $File[0]
        $Item2 = Get-Item -Path $File[1]

        $List = @(
            'DnsClientX-PowerShellModule.v0.4.0.zip',
            'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
        )

        $List | Should -Contain $Item1.Name
        $List | Should -Contain $Item2.Name
    }

    It 'Downloads are fully written to disk' {
        $Dest = Join-Path $TestDrive 'dl-full'
        [array] $File = Save-HTMLAttachment -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$Dest" -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
        foreach ($path in $File) {
            (Get-Item $path).Length | Should -BeGreaterThan 0
        }
    }
}
