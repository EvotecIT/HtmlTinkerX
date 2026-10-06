$corpusAssembly = Get-BenchmarkInput AssemblyPath ''
$assembly = [PSParseHTML.DevelopmentModuleLoadContext.ModuleAssemblyLoadContext]::LoadModule($corpusAssembly, 'HtmlTinkerXBenchmarkCorpus')
$fixtureFactory = $assembly.GetType('HtmlTinkerX.Benchmarks.Corpus').GetMethod('Get')
$coreAssemblyPath = Join-Path (Split-Path $corpusAssembly) 'HtmlTinkerX.dll'
$includeBrowser = Get-BenchmarkInput IncludeBrowser $false -Bool

New-BenchmarkSuite 'htmltinkerx-corpus' -OutputRoot (Join-Path $PSScriptRoot '../../Ignore/Benchmarks') {
    Add-BenchmarkMetadata CoreAssemblySha256 (Get-FileHash -LiteralPath $coreAssemblyPath).Hash
    Add-BenchmarkMetadata CorpusAssemblySha256 (Get-FileHash -LiteralPath $assembly.Location).Hash
    Add-BenchmarkMetadata Scope 'Current-source correctness and timing; no released-version or cross-machine ranking'
    Add-BenchmarkMetadata DomainLabel (Get-BenchmarkInput DomainLabel 'unspecified')
    Add-BenchmarkMetadata Purpose (Get-BenchmarkInput Purpose 'measurement')
    $metadataPath = Get-BenchmarkInput HostMetadataPath ''
    if ($metadataPath) {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        foreach ($property in $metadata.PSObject.Properties) {
            Add-BenchmarkMetadata ('Host' + $property.Name) ($property.Value | ConvertTo-Json -Depth 10 -Compress)
        }
    }
    Set-BenchmarkPolicy -Warmup 5 -Iterations 10 -Order GroupedRotated -MemoryCleanup BeforeIteration -OutlierMode None
    Set-BenchmarkProfile Current
    Add-BenchmarkCaseSource @(
        foreach ($family in 'Nested', 'Tables', 'Cards', 'Unicode', 'StaticRead', 'WarmBrowser', 'Crawl') {
            foreach ($size in $(if ($family -eq 'Crawl') { 10, 50 } elseif ($family -eq 'Tables') { 25, 2500 } else { 25, 250 })) {
                [pscustomobject]@{ Name = "$family-$size"; Family = $family; Size = $size }
            }
        }
    )
    Add-BenchmarkSkipRule {
        param($case, $run)
        if ($case.Family -eq 'WarmBrowser' -and -not $includeBrowser) { 'Browser lane was not selected; use -IncludeBrowser.' }
    }
    Set-BenchmarkSetup {
        param($case, $run)
        $run.Fixture = $fixtureFactory.Invoke($null, @([string]$case.Family, [int]$case.Size))
        $run.Fixture.Prepare()
    }
    Add-BenchmarkEngine HtmlTinkerX {
        Add-BenchmarkOperation Run {
            param($case, $run)
            $run.Fixture.Run()
        }
    }
    Add-BenchmarkValidation {
        param($case, $run)
        $run.Fixture.Validate()
    }
    Add-BenchmarkMetric ValidatedUnits { param($case, $run) $run.Fixture.Units }
}
