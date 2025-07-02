Describe 'BeautifierOptions' {
    It 'Applies custom indentation and brace style - Expand' {
        $Content = 'function x(){return 1;};'
        $Output = Format-JavaScript -Content $Content -IndentSize 2 -BraceStyle Expand

        $Lines = $Output -split "`r?`n"
        $Lines[0] | Should -Be 'function x()'
        $Lines[1] | Should -Be '{'
        $Lines[2] | Should -Be '  return 1;'
        $Lines[3] | Should -Be '};'
    }

    It 'Applies custom indentation and brace style - Collapse' {
        $Content = 'function x(){return 1;};'
        $Output = Format-JavaScript -Content $Content -IndentSize 4 -BraceStyle Collapse

        $Lines = $Output -split "`r?`n"
        $Lines[0] | Should -Be 'function x() {'
        $Lines[1] | Should -Be '    return 1;'
        $Lines[2] | Should -Be '};'
    }
}
