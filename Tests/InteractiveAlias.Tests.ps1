Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Interactive aliases' {
    It 'exports short aliases for common interactive parsing commands' {
        (Get-Command cfhtml).ResolvedCommandName | Should -Be 'ConvertFrom-Html'
        (Get-Command sjsn).ResolvedCommandName | Should -Be 'Select-JavaScriptAstNode'
        (Get-Command sjsdn).ResolvedCommandName | Should -Be 'Select-JavaScriptAstNode'
        (Get-Command sjsv).ResolvedCommandName | Should -Be 'Select-JavaScriptVariable'
    }

    It 'parses HTML through the short ConvertFrom alias' {
        $node = cfhtml -Content '<main><a href="/docs">Docs</a></main>' |
            Select-HtmlNode -Tag a -Single

        $node | Select-HtmlAttributeValue -AttributeName href | Should -Be '/docs'
    }

    It 'supports short JavaScript AST aliases in reusable node pipelines' {
        $classBody = ConvertFrom-JavaScriptAst -Content 'class TicketCipher { constructor() { const key = "abc"; } }' |
            sjsdn -Type ClassBody |
            Select-Object -First 1

        $declaration = $classBody |
            sjsn -Type VariableDeclaration |
            Select-Object -First 1

        $declaration.TypeText | Should -Be 'VariableDeclaration'
    }

    It 'supports short JavaScript variable aliases from AST node input' {
        $ast = ConvertFrom-JavaScriptAst -Content 'const cfg = { token: "abc" };'

        $value = sjsv -InputObject $ast -Name cfg -PropertyPath token

        $value.Value | Should -Be 'abc'
    }
}
