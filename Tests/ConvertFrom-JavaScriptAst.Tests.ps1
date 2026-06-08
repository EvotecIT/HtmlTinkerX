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

    It 'selects loose JavaScript assignments by variable name' {
        $variable = Select-JavaScriptVariable -Source @'
$Config = {
    "fShowPersistentCookiesWarning": false,
    "urlMsaSignUp": "https://example.org",
    "urlMsaLogout": "https://example.com/logout",
    "sCtx": "expected-context"
}
'@ -Name '$Config'

        $variable.Name | Should -Be '$Config'
        $variable.Path | Should -Be '$Config'
        $variable.Kind | Should -Be 'Assignment'
        $variable.Value['fShowPersistentCookiesWarning'] | Should -BeFalse
        $variable.Value['urlMsaLogout'] | Should -Be 'https://example.com/logout'
        $variable.Value['sCtx'] | Should -Be 'expected-context'
        $variable.Node.GetType().FullName | Should -Be 'Acornima.Ast.AssignmentExpression'
    }

    It 'selects values from JavaScript object property paths' {
        $value = Select-JavaScriptVariable -Source @'
$Config = {
    auth: {
        urls: {
            logout: "https://example.com/logout"
        }
    }
}
'@ -Name '$Config' -PropertyPath 'auth.urls.logout'

        $value.Name | Should -Be '$Config'
        $value.Path | Should -Be '$Config'
        $value.PropertyPath | Should -Be 'auth.urls.logout'
        $value.Value | Should -Be 'https://example.com/logout'
    }

    It 'matches JavaScript assignment member paths by name or full path' {
        $script = @'
window.$Config = { sCtx: "from-window" };
globalThis.Config = { sCtx: "from-global" };
'@

        $byName = Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath 'sCtx'
        $byPath = Select-JavaScriptVariable -Source $script -Name 'globalThis.Config' -PropertyPath 'sCtx'

        $byName.Name | Should -Be '$Config'
        $byName.Path | Should -Be 'window.$Config'
        $byName.Value | Should -Be 'from-window'
        $byPath.Name | Should -Be 'Config'
        $byPath.Path | Should -Be 'globalThis.Config'
        $byPath.Value | Should -Be 'from-global'
    }

    It 'returns each matching JavaScript assignment occurrence in source order' {
        $values = @(Select-JavaScriptVariable -Source @'
$Config = { sCtx: "first" };
$Config = { sCtx: "second" };
'@ -Name '$Config' -PropertyPath 'sCtx')

        $values.Count | Should -Be 2
        $values[0].Kind | Should -Be 'Assignment'
        $values[0].Value | Should -Be 'first'
        $values[1].Kind | Should -Be 'Assignment'
        $values[1].Value | Should -Be 'second'
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

    It 'can include the AST root when selecting nodes' {
        $node = Select-JavaScriptAstNode -Source 'const settings = { apiKey: "abc" };' -Type Script -IncludeRoot |
            Select-Object -First 1

        $node.GetType().FullName | Should -Be 'Acornima.Ast.Script'
    }
}
