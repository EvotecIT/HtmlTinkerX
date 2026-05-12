Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'JavaScript AST cmdlets' {
    It 'parses JavaScript into an Acornima AST' {
        $ast = ConvertFrom-JavaScriptAst -Content 'const answer = 42;'

        $ast | Should -Not -BeNullOrEmpty
        $ast.GetType().FullName | Should -Be 'Acornima.Ast.Script'
    }

    It 'selects JavaScript variable declarations by name' {
        $variable = ConvertFrom-JavaScriptAst -Content 'const answer = 42; let tokenValue = "abc";' |
            Select-JavaScriptVariable -Name answer

        $variable.Name | Should -Be 'answer'
        $variable.Kind | Should -Be 'Const'
        $variable.Value | Should -Be 42
        $variable.RawValue | Should -Be '42'
    }

    It 'selects JavaScript variable declarations by prefix' {
        $variable = Select-JavaScriptVariable -Source 'const answer = 42; let tokenValue = "abc";' -Name token -StartsWith

        $variable.Name | Should -Be 'tokenValue'
        $variable.Value | Should -Be 'abc'
    }
}
