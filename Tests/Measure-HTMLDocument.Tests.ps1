Import-Module (Resolve-Path "$PSScriptRoot/../Sources/PSParseHTML.PowerShell/bin/Release/net8.0/PSParseHTML.PowerShell.dll") -Force

Describe 'Measure-HTMLDocument' {
    It 'Should return counts for html string' {
        $html = '<html><body><p>Hello world</p><a href="#">link</a><img src="i.png" /></body></html>'
        $stats = Measure-HTMLDocument -Content $html
        $stats.WordCount | Should -Be 3
        $stats.LinkCount | Should -Be 1
        $stats.ImageCount | Should -Be 1
    }
}
