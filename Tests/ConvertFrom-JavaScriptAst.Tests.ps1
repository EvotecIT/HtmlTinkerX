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

    It 'selects descendant AST nodes by Acornima type' {
        $nodes = ConvertFrom-JavaScriptAst -Content 'class App { run() { const answer = 42; } }' |
            Select-JavaScriptAstNode -Type ClassBody, VariableDeclaration

        $nodes.TypeText | Should -Contain 'ClassBody'
        $nodes.TypeText | Should -Contain 'VariableDeclaration'
    }

    It 'can parse source directly when selecting AST nodes' {
        $node = Select-JavaScriptAstNode -Source 'const settings = { apiKey: "abc" };' -Type ObjectExpression |
            Select-Object -First 1

        $node.TypeText | Should -Be 'ObjectExpression'
    }
}
