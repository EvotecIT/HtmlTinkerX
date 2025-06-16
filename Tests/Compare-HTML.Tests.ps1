Describe 'Compare-HTML' {
    It 'Detects difference between fragments' {
        $reference = '<div><p>A</p></div>'
        $difference = '<div><p>B</p></div>'
        $result = Compare-HTML -Reference $reference -Difference $difference
        ($result | Measure-Object).Count | Should -BeGreaterThan 0
    }

    It 'Returns nothing for identical input' {
        $html = '<span>Same</span>'
        $result = Compare-HTML -Reference $html -Difference $html
        ($result | Measure-Object).Count | Should -Be 0
    }
}
