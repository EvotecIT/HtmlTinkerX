Describe 'Packaged AssemblyLoadContext conflict isolation' {
    It 'loads after TextMate and PwshSpectreConsole without sharing the default ALC' {
        $packagedModuleRoot = Join-Path $PSScriptRoot '..\Artefacts\Unpacked\Modules'
        $packagedModule = Join-Path $packagedModuleRoot 'PSParseHTML'
        $packagedLoader = Join-Path $packagedModule 'Lib\Core\PSParseHTML.ModuleLoadContext.dll'
        $hasConflictModules = (Get-Module -ListAvailable -Name TextMate | Where-Object Version -eq '0.2.1') -and
            (Get-Module -ListAvailable -Name PwshSpectreConsole | Where-Object Version -eq '2.6.3')
        if ($PSVersionTable.PSEdition -ne 'Core' -or -not (Test-Path -LiteralPath $packagedLoader) -or -not $hasConflictModules) {
            Set-ItResult -Skipped -Because 'packaged Core artifact and conflict modules are required'
            return
        }

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module TextMate -RequiredVersion 0.2.1 -Force
Import-Module PwshSpectreConsole -RequiredVersion 2.6.3 -Force
`$textMateBefore = (Get-Command Get-TextMateGrammar -ErrorAction Stop).Count
`$spectreBefore = (Get-Command Get-SpectreColors -ErrorAction Stop).Count

Import-Module PSParseHTML -Force
`$table = ConvertFrom-HtmlTable -Content '<table><tr><th>Name</th></tr><tr><td>42</td></tr></table>' -Engine AngleSharp
`$command = Get-Command ConvertFrom-HtmlTable -ErrorAction Stop
`$commandAssembly = `$command.ImplementingType.Assembly
`$commandAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$commandAssembly)
`$loadedAssemblies = [System.Runtime.Loader.AssemblyLoadContext]::All |
    ForEach-Object {
        `$alc = `$_
        foreach (`$assembly in `$alc.Assemblies) {
            if (`$assembly.GetName().Name -in @('PSParseHTML.PowerShell', 'HtmlTinkerX', 'SixLabors.ImageSharp', 'Spectre.Console')) {
                [pscustomobject]@{
                    Assembly = `$assembly.GetName().Name
                    Version = `$assembly.GetName().Version.ToString()
                    ALC = `$alc.Name
                    IsDefault = [object]::ReferenceEquals(`$alc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
                }
            }
        }
    }

[pscustomobject]@{
    TextMateCommandBefore = `$textMateBefore
    SpectreCommandBefore = `$spectreBefore
    TextMateCommandAfter = (Get-Command Get-TextMateGrammar -ErrorAction Stop).Count
    SpectreCommandAfter = (Get-Command Get-SpectreColors -ErrorAction Stop).Count
    TableValue = `$table.Name
    ConvertFromHtmlTableAssembly = `$commandAssembly.Location
    ConvertFromHtmlTableALC = `$commandAlc.Name
    ConvertFromHtmlTableALCIsDefault = [object]::ReferenceEquals(`$commandAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    LoadedAssemblies = @(`$loadedAssemblies)
} | ConvertTo-Json -Depth 6 -Compress
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $output = pwsh -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)

        $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
        $json | Should -Not -BeNullOrEmpty -Because ($output -join [Environment]::NewLine)
        $result = $json | ConvertFrom-Json

        $result.TextMateCommandBefore | Should -BeGreaterOrEqual 1
        $result.SpectreCommandBefore | Should -BeGreaterOrEqual 1
        $result.TextMateCommandAfter | Should -BeGreaterOrEqual 1
        $result.SpectreCommandAfter | Should -BeGreaterOrEqual 1
        $result.TableValue | Should -Be '42'
        $result.ConvertFromHtmlTableAssembly | Should -BeLike '*\Artefacts\Unpacked\Modules\PSParseHTML\Lib\Core\PSParseHTML.PowerShell.dll'
        $result.ConvertFromHtmlTableALC | Should -Be 'PSParseHTML'
        $result.ConvertFromHtmlTableALCIsDefault | Should -BeFalse

        $loadedAssemblies = @($result.LoadedAssemblies)
        $psParseHtmlAssembly = $loadedAssemblies | Where-Object Assembly -eq 'PSParseHTML.PowerShell' | Select-Object -First 1
        $htmlTinkerXAssembly = $loadedAssemblies | Where-Object Assembly -eq 'HtmlTinkerX' | Select-Object -First 1
        $spectreAssembly = $loadedAssemblies | Where-Object Assembly -eq 'Spectre.Console' | Select-Object -First 1

        $psParseHtmlAssembly.ALC | Should -Be 'PSParseHTML'
        $psParseHtmlAssembly.IsDefault | Should -BeFalse
        $htmlTinkerXAssembly.ALC | Should -Be 'PSParseHTML'
        $htmlTinkerXAssembly.IsDefault | Should -BeFalse
        $spectreAssembly.IsDefault | Should -BeTrue
    }
}
