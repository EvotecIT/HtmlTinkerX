Describe 'Measure-HTMLDocument' {
    It 'Should return counts for html string' {
        $html = '<html><body><p>Hello world</p><a href="#">link</a><img src="i.png" /></body></html>'
        $stats = Measure-HTMLDocument -Content $html
        $stats.WordCount | Should -Be 3
        $stats.LinkCount | Should -Be 1
        $stats.ImageCount | Should -Be 1
    }
}
