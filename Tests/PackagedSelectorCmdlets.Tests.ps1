Describe 'Packaged selector cmdlets' {
    It 'exports selector cmdlets and dependency type accelerators from the packaged module' {
        $packagedModuleRoot = Join-Path $PSScriptRoot '..\Artefacts\Unpacked\Modules'
        $packagedModule = Join-Path $packagedModuleRoot 'PSParseHTML'
        $packagedLoader = Join-Path $packagedModule 'Lib\Core\PSParseHTML.ModuleLoadContext.dll'
        if ($PSVersionTable.PSEdition -ne 'Core' -or -not (Test-Path -LiteralPath $packagedLoader)) {
            Set-ItResult -Skipped -Because 'packaged Core artifact is required'
            return
        }

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module PSParseHTML -Force
`$doc = ConvertFrom-HTML -Content '<main><a href="/reports/42" data-kind="report">Report &amp; Status</a></main>'
`$link = `$doc | Select-HtmlNode -Tag a -AttributeName data-kind -AttributeValue report -Single
`$href = `$link | Select-HtmlAttributeValue -AttributeName href
`$text = `$link | Select-HtmlInnerText -DeEntitize
`$staticLink = Select-HtmlElement -Content '<main><a href="/reports/43">Static report</a></main>' -Selector 'main > a' -First
`$records = Select-HtmlData -Content '<main><article class="card"><h2>First</h2><a href="/first">Open</a></article><article class="card"><h2>Second</h2><a href="/second">Open</a></article></main>' -ItemSelector 'article.card' -Property @{
    Name = 'h2'
    Link = @{ Selector = 'a'; Attribute = 'href'; ResolveUrl = `$true }
} -BaseUrl 'https://example.test/catalog/'
`$discovery = Find-HtmlSelector -Content '<main><article class="card"><h2>First</h2><a href="/first">Open</a></article><article class="card"><h2>Second</h2><a href="/second">Open</a></article></main>' -Query First -Limit 1
`$variable = ConvertFrom-JavaScriptAst -Content 'const reportToken = "abc";' | Select-JavaScriptVariable -Name reportToken
`$objectExpression = ConvertFrom-JavaScriptAst -Content 'const settings = { apiKey: "abc" };' | Select-JavaScriptAstNode -Type ObjectExpression | Select-Object -First 1

[pscustomobject]@{
    Commands = @(
        Get-Command Find-HtmlSelector, Select-HtmlElement, Select-HtmlData, Select-HtmlNode, Select-HtmlAttributeValue, Select-HtmlInnerText, ConvertFrom-JavaScriptAst, Select-JavaScriptAstNode, Select-JavaScriptVariable |
            Select-Object -ExpandProperty Name
    )
    Href = `$href
    Text = `$text
    StaticHref = `$staticLink | Select-HtmlAttributeValue -AttributeName href
    RecordCount = @(`$records).Count
    SecondRecordName = @(`$records)[1].Name
    SecondRecordLink = @(`$records)[1].Link
    DiscoverySelector = `$discovery.Selector
    DiscoveryCommand = `$discovery.SuggestedCommand
    JavaScriptVariableName = `$variable.Name
    JavaScriptVariableValue = `$variable.Value
    JavaScriptAstNodeType = `$objectExpression.TypeText
    HtmlNodeAccelerator = [HtmlAgilityPack.HtmlNode].FullName
    HtmlNodeTypeAccelerator = [HtmlAgilityPack.HtmlNodeType].FullName
    AcornimaScriptAccelerator = [Acornima.Ast.Script].FullName
} | ConvertTo-Json -Depth 4 -Compress
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $output = pwsh -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)

        $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
        $json | Should -Not -BeNullOrEmpty -Because ($output -join [Environment]::NewLine)
        $result = $json | ConvertFrom-Json

        $result.Commands | Should -Contain 'Select-HtmlNode'
        $result.Commands | Should -Contain 'Select-HtmlAttributeValue'
        $result.Commands | Should -Contain 'Select-HtmlInnerText'
        $result.Commands | Should -Contain 'Find-HtmlSelector'
        $result.Commands | Should -Contain 'Select-HtmlElement'
        $result.Commands | Should -Contain 'Select-HtmlData'
        $result.Commands | Should -Contain 'ConvertFrom-JavaScriptAst'
        $result.Commands | Should -Contain 'Select-JavaScriptAstNode'
        $result.Commands | Should -Contain 'Select-JavaScriptVariable'
        $result.Href | Should -Be '/reports/42'
        $result.Text | Should -Be 'Report & Status'
        $result.StaticHref | Should -Be '/reports/43'
        $result.RecordCount | Should -Be 2
        $result.SecondRecordName | Should -Be 'Second'
        $result.SecondRecordLink | Should -Be 'https://example.test/second'
        $result.DiscoverySelector | Should -Be 'article.card'
        $result.DiscoveryCommand | Should -Match 'Select-HtmlData'
        $result.JavaScriptVariableName | Should -Be 'reportToken'
        $result.JavaScriptVariableValue | Should -Be 'abc'
        $result.JavaScriptAstNodeType | Should -Be 'ObjectExpression'
        $result.HtmlNodeAccelerator | Should -Be 'HtmlAgilityPack.HtmlNode'
        $result.HtmlNodeTypeAccelerator | Should -Be 'HtmlAgilityPack.HtmlNodeType'
        $result.AcornimaScriptAccelerator | Should -Be 'Acornima.Ast.Script'
    }
}
