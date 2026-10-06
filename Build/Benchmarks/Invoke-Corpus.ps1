#requires -Version 7.4
[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot '../../Ignore/Benchmarks'),
    [string[]] $Case,
    [int] $WarmupCount = 5,
    [int] $IterationCount = 10,
    [switch] $IncludeBrowser,
    [switch] $Plan,
    [switch] $SkipBuild,
    [UInt64] $ProcessorAffinityMask,
    [ValidateSet('Idle', 'BelowNormal', 'Normal', 'AboveNormal', 'High')] [string] $ProcessPriority = 'Normal',
    [string] $DomainLabel = 'unspecified',
    [string] $HostMetadataPath,
    [ValidateSet('measurement', 'correctness-smoke')] [string] $Purpose = 'measurement'
)
$ErrorActionPreference = 'Stop'
Import-Module PSPublishModule -MinimumVersion 3.0.156 -ErrorAction Stop
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SkipBuild) {
    & dotnet build (Join-Path $PSScriptRoot 'Corpus/HtmlTinkerX.Benchmarks.csproj') -c Release -p:TargetFrameworks=net8.0 -p:UseSharedCompilation=false | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Corpus build failed.' }
}
$previousDevelopmentMode = $env:PSPARSEHTML_USE_DEVELOPMENT_BINARIES
$previousConfiguration = $env:PSPARSEHTML_DEVELOPMENT_CONFIGURATION
$corpusType = $null
try {
    $env:PSPARSEHTML_USE_DEVELOPMENT_BINARIES = 'true'
    $env:PSPARSEHTML_DEVELOPMENT_CONFIGURATION = 'Release'
    Import-Module (Join-Path $repositoryRoot 'PSParseHTML.psd1') -Force
    $corpusPath = Join-Path $PSScriptRoot 'Corpus/bin/Release/net8.0/HtmlTinkerX.Benchmarks.dll'
    $corpusType = [PSParseHTML.DevelopmentModuleLoadContext.ModuleAssemblyLoadContext]::LoadModule($corpusPath, 'HtmlTinkerXBenchmarkCorpus').GetType('HtmlTinkerX.Benchmarks.Corpus')
    $invoke = @{
        Path = (Join-Path $PSScriptRoot 'corpus.benchmark.ps1'); OutputRoot = $OutputRoot
        WarmupCount = $WarmupCount; IterationCount = $IterationCount; Plan = $Plan
        Variable = @{
            AssemblyPath = $corpusPath
            IncludeBrowser = [bool]$IncludeBrowser; DomainLabel = $DomainLabel; HostMetadataPath = $HostMetadataPath
            Purpose = $Purpose
        }
    }
    if ($Case) { $invoke.Case = $Case }
    if ($PSBoundParameters.ContainsKey('ProcessPriority')) { $invoke.ProcessPriority = $ProcessPriority }
    if ($PSBoundParameters.ContainsKey('ProcessorAffinityMask')) { $invoke.ProcessorAffinityMask = $ProcessorAffinityMask }
    $result = Invoke-BenchmarkSuite @invoke
    if (-not $Plan -and @($result.Samples | Where-Object { $_.Status -eq 'Failed' }).Count) {
        throw "Corpus validation failed. Inspect the native results under $OutputRoot."
    }
    $result
}
finally {
    try { if ($corpusType) { [void]$corpusType.GetMethod('DisposeAll').Invoke($null, @()) } }
    finally {
        $env:PSPARSEHTML_USE_DEVELOPMENT_BINARIES = $previousDevelopmentMode
        $env:PSPARSEHTML_DEVELOPMENT_CONFIGURATION = $previousConfiguration
    }
}
