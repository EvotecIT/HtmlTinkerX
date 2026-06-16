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
        if (-not $manifest.ExportedCmdlets.ContainsKey('Get-HtmlBrowserDiagnostics')) {
            Set-ItResult -Skipped -Because 'packaged artifact predates browser extraction mode commands'
            return
        }

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module PSParseHTML -Force
`$commands = Get-Command Get-HtmlBrowserDiagnostics, Get-HtmlBrowserElement, Test-HtmlBrowserElement, Get-HtmlBrowserActiveElement, Get-HtmlBrowserStorage, Set-HtmlBrowserStorage, Save-HtmlBrowserContent, Invoke-HtmlBrowserHover, Invoke-HtmlBrowserKey, Invoke-HtmlBrowserOverlayDismissal, Invoke-HtmlBrowserScroll, Wait-HtmlBrowserContent, Set-HtmlBrowserInput, Invoke-HtmlBrowserClick, Find-HtmlDataSource, Invoke-HtmlDataExtraction, Export-HtmlExtractionRecipe, Import-HtmlExtractionRecipe, Invoke-HtmlExtractionRecipe |
    Select-Object -ExpandProperty Name
`$aliases = Get-Alias Get-HtmlDiagnostics, Get-HtmlElement, Test-HtmlElement, Get-HtmlActiveElement, Get-HtmlStorage, Set-HtmlStorage, Save-HtmlContent, Invoke-HtmlHover, Invoke-HtmlKey, Invoke-HtmlOverlayDismissal, Invoke-HtmlScroll, Wait-HtmlContent, Set-HtmlInput, Invoke-HtmlClick |
    Select-Object Name, Definition
`$help = Get-Help Get-HtmlBrowserDiagnostics -Examples | Out-String

[pscustomobject]@{
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

        $result.Commands | Should -Contain 'Get-HtmlBrowserDiagnostics'
        $result.Commands | Should -Contain 'Get-HtmlBrowserElement'
        $result.Commands | Should -Contain 'Test-HtmlBrowserElement'
        $result.Commands | Should -Contain 'Get-HtmlBrowserStorage'
        $result.Commands | Should -Contain 'Save-HtmlBrowserContent'
        $result.Commands | Should -Contain 'Wait-HtmlBrowserContent'
        $result.Commands | Should -Contain 'Find-HtmlDataSource'
        $result.Commands | Should -Contain 'Invoke-HtmlDataExtraction'
        $result.Commands | Should -Contain 'Export-HtmlExtractionRecipe'
        $result.Commands | Should -Contain 'Import-HtmlExtractionRecipe'
        $result.Commands | Should -Contain 'Invoke-HtmlExtractionRecipe'
        ($result.Aliases | Where-Object Name -eq 'Get-HtmlDiagnostics').Definition | Should -Be 'Get-HtmlBrowserDiagnostics'
        ($result.Aliases | Where-Object Name -eq 'Get-HtmlElement').Definition | Should -Be 'Get-HtmlBrowserElement'
        ($result.Aliases | Where-Object Name -eq 'Save-HtmlContent').Definition | Should -Be 'Save-HtmlBrowserContent'
        ($result.Aliases | Where-Object Name -eq 'Wait-HtmlContent').Definition | Should -Be 'Wait-HtmlBrowserContent'
        $result.DiagnosticsHelp | Should -Match 'Get-HtmlBrowserDiagnostics'
        $result.DiagnosticsHelp | Should -Match 'ObservedApiCalls'
    }
}
