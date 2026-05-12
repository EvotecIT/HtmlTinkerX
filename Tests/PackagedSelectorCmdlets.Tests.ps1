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
`$variable = ConvertFrom-JavaScriptAst -Content 'const reportToken = "abc";' | Select-JavaScriptVariable -Name reportToken

[pscustomobject]@{
    Commands = @(
        Get-Command Select-HtmlNode, Select-HtmlAttributeValue, Select-HtmlInnerText, ConvertFrom-JavaScriptAst, Select-JavaScriptVariable |
            Select-Object -ExpandProperty Name
    )
    Href = `$href
    Text = `$text
    JavaScriptVariableName = `$variable.Name
    JavaScriptVariableValue = `$variable.Value
    HtmlNodeAccelerator = [HtmlAgilityPack.HtmlNode].FullName
    AcornimaParserAccelerator = [Acornima.Parser].FullName
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
        $result.Commands | Should -Contain 'ConvertFrom-JavaScriptAst'
        $result.Commands | Should -Contain 'Select-JavaScriptVariable'
        $result.Href | Should -Be '/reports/42'
        $result.Text | Should -Be 'Report & Status'
        $result.JavaScriptVariableName | Should -Be 'reportToken'
        $result.JavaScriptVariableValue | Should -Be 'abc'
        $result.HtmlNodeAccelerator | Should -Be 'HtmlAgilityPack.HtmlNode'
        $result.AcornimaParserAccelerator | Should -Be 'Acornima.Parser'
    }
}
