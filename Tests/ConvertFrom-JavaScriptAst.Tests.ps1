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

    It 'preserves source order across JavaScript declarations and assignments' {
        $values = @(Select-JavaScriptVariable -Source @'
$Config = { sCtx: "assigned-first" };
var $Config = { sCtx: "declared-second" };
'@ -Name '$Config' -PropertyPath 'sCtx')

        $values.Count | Should -Be 2
        $values[0].Kind | Should -Be 'Assignment'
        $values[0].Value | Should -Be 'assigned-first'
        $values[1].Kind | Should -Be 'Var'
        $values[1].Value | Should -Be 'declared-second'
    }

    It 'skips compound assignments because their value depends on previous state' {
        $values = @(Select-JavaScriptVariable -Source @'
$Config += suffix;
$Config = { sCtx: "final" };
'@ -Name '$Config' -PropertyPath 'sCtx')

        $values.Count | Should -Be 1
        $values[0].Kind | Should -Be 'Assignment'
        $values[0].Value | Should -Be 'final'
    }

    It 'does not convert dynamic computed member assignments to static paths' {
        $dynamic = @(Select-JavaScriptVariable -Source @'
window[key] = { sCtx: "dynamic" };
window["$Config"] = { sCtx: "literal" };
'@ -Name '$Config' -PropertyPath 'sCtx')

        $dynamic.Count | Should -Be 1
        $dynamic[0].Path | Should -Be 'window.$Config'
        $dynamic[0].Value | Should -Be 'literal'

        $key = @(Select-JavaScriptVariable -Source 'window[key] = { sCtx: "dynamic" };' -Name key)
        $key.Count | Should -Be 0
    }

    It 'does not throw when unary minus cannot be statically converted to a number' {
        { $script:unary = Select-JavaScriptVariable -Source 'const value = -"abc";' -Name value } | Should -Not -Throw
        $script:unary.Name | Should -Be 'value'
        $script:unary.Value | Should -BeNullOrEmpty
    }

    It 'does not report a concrete value for unary not over runtime expressions' {
        $variable = Select-JavaScriptVariable -Source 'const enabled = !window.disabled;' -Name enabled

        $variable.Name | Should -Be 'enabled'
        $variable.Value | Should -BeNullOrEmpty
    }

    It 'does not treat dynamic computed object keys as static property paths' {
        $values = @(Select-JavaScriptVariable -Source @'
const cfg = {
    [key]: "dynamic",
    staticKey: "literal"
};
'@ -Name cfg -PropertyPath key, staticKey)

        $values.Count | Should -Be 2
        $values[0].PropertyPath | Should -Be 'key'
        $values[0].Value | Should -BeNullOrEmpty
        $values[1].PropertyPath | Should -Be 'staticKey'
        $values[1].Value | Should -Be 'literal'
    }

    It 'treats object spreads as unknown overrides for earlier properties' {
        $values = @(Select-JavaScriptVariable -Source @'
const cfg = {
    token: "old",
    ...override,
    safe: "after"
};
'@ -Name cfg -PropertyPath token, safe)

        $values.Count | Should -Be 2
        $values[0].PropertyPath | Should -Be 'token'
        $values[0].Value | Should -BeNullOrEmpty
        $values[1].PropertyPath | Should -Be 'safe'
        $values[1].Value | Should -Be 'after'
    }

    It 'does not index arrays past a spread element as fixed positions' {
        $values = @(Select-JavaScriptVariable -Source @'
const cfg = {
    items: ["first", ...extra, "last"]
};
'@ -Name cfg -PropertyPath items.0, items.2)

        $values.Count | Should -Be 2
        $values[0].PropertyPath | Should -Be 'items.0'
        $values[0].Value | Should -Be 'first'
        $values[1].PropertyPath | Should -Be 'items.2'
        $values[1].Value | Should -BeNullOrEmpty
    }

    It 'selects JavaScript variables directly from HTML script tags' {
        $values = @(Select-HtmlJavaScriptVariable -Content @'
<html>
<head>
<script type="application/ld+json">{"name":"schema"}</script>
<script type="text/javascript">
window.$Config = {
    auth: {
        sCtx: "expected-context",
        urls: {
            logout: "https://example.com/logout"
        }
    }
};
</script>
</head>
</html>
'@ -Name '$Config' -PropertyPath 'auth.sCtx','auth.urls.logout')

        $values.Count | Should -Be 2
        $values[0].Name | Should -Be '$Config'
        $values[0].Path | Should -Be 'window.$Config'
        $values[0].ScriptIndex | Should -Be 1
        $values[0].ScriptType | Should -Be 'text/javascript'
        $values[0].PropertyPath | Should -Be 'auth.sCtx'
        $values[0].Value | Should -Be 'expected-context'
        $values[1].PropertyPath | Should -Be 'auth.urls.logout'
        $values[1].Value | Should -Be 'https://example.com/logout'
    }

    It 'parses HTML module scripts with the module grammar' {
        $value = Select-HtmlJavaScriptVariable -Content @'
<script type="module">
import value from "./settings.js";
window.$Config = { sCtx: "from-module" };
</script>
<script>
window.$Config = { sCtx: "from-script" };
</script>
'@ -Name '$Config' -PropertyPath sCtx |
            Select-Object -First 1

        $value.Path | Should -Be 'window.$Config'
        $value.ScriptIndex | Should -Be 0
        $value.ScriptType | Should -Be 'module'
        $value.Value | Should -Be 'from-module'
    }

    It 'returns each matching HTML JavaScript assignment occurrence in source order' {
        $values = @(Select-HtmlJavaScriptVariable -Content @'
<script>
$Config = { sCtx: "first" };
$Config = { sCtx: "second" };
</script>
'@ -Name '$Config' -PropertyPath 'sCtx')

        $values.Count | Should -Be 2
        $values[0].Value | Should -Be 'first'
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
