Describe 'Packaged browser extraction mode' {
    It 'exports browser extraction commands, aliases, and help from the packaged module' {
        $packagedModuleRoot = Join-Path $PSScriptRoot '..\Artefacts\Unpacked\Modules'
        $packagedModule = Join-Path $packagedModuleRoot 'PSParseHTML'
        $packagedManifest = Join-Path $packagedModule 'PSParseHTML.psd1'
        $packagedLoader = Join-Path $packagedModule 'Lib\Core\PSParseHTML.ModuleLoadContext.dll'
        if ($PSVersionTable.PSEdition -ne 'Core' -or -not (Test-Path -LiteralPath $packagedLoader)) {
            Set-ItResult -Skipped -Because 'packaged Core artifact is required'
            return
        }

        $manifest = Test-ModuleManifest -Path $packagedManifest -ErrorAction Stop
        if (-not $manifest.ExportedCmdlets.ContainsKey('Get-HtmlBrowserDiagnostics') -or -not $manifest.ExportedCmdlets.ContainsKey('Export-HtmlBrowserEvidence')) {
            Set-ItResult -Skipped -Because 'packaged artifact predates current browser automation commands'
            return
        }

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module PSParseHTML -Force
`$expectedCommands = @(
    'Start-HtmlBrowserSession',
    'New-HtmlBrowserProfile',
    'Import-HtmlBrowserProfile',
    'Export-HtmlBrowserProfile',
    'Wait-HtmlBrowserReady',
    'Find-HtmlBrowserLocator',
    'Export-HtmlBrowserEvidence',
    'Get-HtmlBrowserDiagnostics',
    'Get-HtmlBrowserElement',
    'Test-HtmlBrowserElement',
    'Get-HtmlBrowserActiveElement',
    'Get-HtmlBrowserStorage',
    'Set-HtmlBrowserStorage',
    'Save-HtmlBrowserContent',
    'Invoke-HtmlBrowserHover',
    'Invoke-HtmlBrowserKey',
    'Close-HtmlBrowserOverlay',
    'Invoke-HtmlBrowserScroll',
    'Wait-HtmlBrowserContent',
    'Set-HtmlBrowserInput',
    'Invoke-HtmlBrowserClick',
    'Find-HtmlDataSource',
    'Find-HtmlBrowserDataSource',
    'Get-HtmlBrowserSsoHandoff',
    'Invoke-HtmlDataExtraction',
    'Export-HtmlExtractionRecipe',
    'Import-HtmlExtractionRecipe',
    'Invoke-HtmlExtractionRecipe',
    'Start-HtmlBrowserRecipeRecording',
    'Stop-HtmlBrowserRecipeRecording',
    'Export-HtmlBrowserRecipe',
    'Invoke-HtmlBrowserRecipe'
)
`$expectedAliases = @(
    'Start-HtmlSession',
    'Open-HtmlSession',
    'Wait-HtmlReady',
    'Get-HtmlSsoHandoff',
    'Get-HtmlDiagnostics',
    'Get-HtmlElement',
    'Test-HtmlElement',
    'Get-HtmlActiveElement',
    'Get-HtmlStorage',
    'Set-HtmlStorage',
    'Save-HtmlContent',
    'Invoke-HtmlHover',
    'Invoke-HtmlKey',
    'Invoke-HtmlOverlayDismissal',
    'Invoke-HtmlScroll',
    'Wait-HtmlContent',
    'Set-HtmlInput',
    'Invoke-HtmlClick'
)
`$commands = Get-Command -Name `$expectedCommands |
    Select-Object -ExpandProperty Name
`$aliases = Get-Alias -Name `$expectedAliases |
    Select-Object Name, Definition
`$help = Get-Help Get-HtmlBrowserDiagnostics -Examples | Out-String

[pscustomobject]@{
    ExpectedCommands = @(`$expectedCommands)
    ExpectedAliases = @(`$expectedAliases)
    Commands = @(`$commands)
    Aliases = @(`$aliases)
    DiagnosticsHelp = `$help
} | ConvertTo-Json -Depth 5 -Compress
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $output = pwsh -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)

        $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
        $json | Should -Not -BeNullOrEmpty -Because ($output -join [Environment]::NewLine)
        $result = $json | ConvertFrom-Json

        foreach ($commandName in $result.ExpectedCommands) {
            $result.Commands | Should -Contain $commandName
        }
        ($result.Aliases | Where-Object Name -eq 'Get-HtmlDiagnostics').Definition | Should -Be 'Get-HtmlBrowserDiagnostics'
        ($result.Aliases | Where-Object Name -eq 'Get-HtmlElement').Definition | Should -Be 'Get-HtmlBrowserElement'
        ($result.Aliases | Where-Object Name -eq 'Save-HtmlContent').Definition | Should -Be 'Save-HtmlBrowserContent'
        ($result.Aliases | Where-Object Name -eq 'Wait-HtmlContent').Definition | Should -Be 'Wait-HtmlBrowserContent'
        ($result.Aliases | Where-Object Name -eq 'Start-HtmlSession').Definition | Should -Be 'Start-HtmlBrowserSession'
        ($result.Aliases | Where-Object Name -eq 'Open-HtmlSession').Definition | Should -Be 'Start-HtmlBrowserSession'
        ($result.Aliases | Where-Object Name -eq 'Wait-HtmlReady').Definition | Should -Be 'Wait-HtmlBrowserReady'
        ($result.Aliases | Where-Object Name -eq 'Get-HtmlSsoHandoff').Definition | Should -Be 'Get-HtmlBrowserSsoHandoff'
        $result.DiagnosticsHelp | Should -Match 'Get-HtmlBrowserDiagnostics'
        $result.DiagnosticsHelp | Should -Match 'ObservedApiCalls'
    }
}
