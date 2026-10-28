#Requires -Version 7.4
<#
.SYNOPSIS
Runs the Windows test target once and records runner state while it executes.
.DESCRIPTION
Keeps the test exit code, protocol diagnostics and two-minute blame deadline.
Snapshots only the test process tree, TCP endpoints and install-lock metadata;
it does not acquire a cache lock or change browser/cache state.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('net8.0', 'net10.0', 'net472')][string] $Framework,
    [string] $Configuration = 'Release',
    [string] $Verbosity = 'minimal',
    [string] $ResultsDirectory = 'Sources/HtmlTinkerX.Tests/TestResults',
    [string] $Filter,
    [TimeSpan] $HangTimeout = '00:02:00'
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$results = [IO.Path]::GetFullPath($ResultsDirectory)
$logs = Join-Path $results 'browser-diagnostics'
$null = New-Item -ItemType Directory -Path $logs -Force
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
# The .NET transport logs the API-channel error before cleanup can disconnect it.
$env:DEBUG = "$env:DEBUG,pw:channel:send,pw:channel:recv"
$diagnostics = Join-Path $logs "runner-$Framework.jsonl"

function Write-RunnerSnapshot {
    param([Diagnostics.Process] $TestRunner)
    try {
        $processes = @(Get-CimInstance Win32_Process)
        $ids = [Collections.Generic.HashSet[int]]::new()
        $null = $ids.Add($TestRunner.Id)
        do {
            $added = $false
            foreach ($process in $processes) {
                if ($ids.Contains([int]$process.ParentProcessId) -and $ids.Add([int]$process.ProcessId)) { $added = $true }
            }
        } while ($added)
        $tree = @($processes | Where-Object { $ids.Contains([int]$_.ProcessId) } | Select-Object `
            ProcessId, ParentProcessId, Name, CreationDate, CommandLine, KernelModeTime, UserModeTime, WorkingSetSize, HandleCount, ThreadCount)
        $ports = @(& netstat.exe -ano -p tcp | Where-Object { $_ -match '\s(\d+)\s*$' -and $ids.Contains([int]$Matches[1]) })
        $locks = @()
        $lockPaths = @((Join-Path $env:LOCALAPPDATA 'HtmlTinkerX/playwright-install.lock'))
        if ($env:HTMLTINKERX_INSTALL_LOCK_PATH) { $lockPaths += $env:HTMLTINKERX_INSTALL_LOCK_PATH }
        # Cache tests use their own top-level temporary lock paths.
        foreach ($directory in Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'htmltinkerx-*' |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 30) {
            $lockPaths += @(Get-ChildItem -LiteralPath $directory.FullName -File -Filter '*.lock.owner.json' |
                ForEach-Object { $_.FullName.Substring(0, $_.FullName.Length - '.owner.json'.Length) })
        }
        foreach ($lockPath in $lockPaths | Select-Object -Unique) {
            foreach ($suffix in '.owner.json', '.writer.json') {
                $path = $lockPath + $suffix
                if (Test-Path -LiteralPath $path -PathType Leaf) {
                    try {
                        $stream = [IO.FileStream]::new($path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
                            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
                        try {
                            $reader = [IO.StreamReader]::new($stream)
                            try { $locks += @{ Path = $path; Metadata = $reader.ReadToEnd() } }
                            finally { $reader.Dispose() }
                        } finally { $stream.Dispose() }
                    }
                    catch { $locks += @{ Path = $path; Error = $_.Exception.Message } }
                }
            }
        }
        $os = Get-CimInstance Win32_OperatingSystem
        @{ Utc = [DateTime]::UtcNow.ToString('o'); RunnerPid = $TestRunner.Id; Processes = $tree; Tcp = $ports;
            Locks = $locks; FreePhysicalMemoryKiB = $os.FreePhysicalMemory; FreeVirtualMemoryKiB = $os.FreeVirtualMemory } |
            ConvertTo-Json -Depth 7 -Compress | Add-Content -LiteralPath $diagnostics -Encoding utf8
    } catch {
        # Diagnostics must not replace the test result.
        try { "$(Get-Date -Format o) $($_.Exception)" | Add-Content -LiteralPath (Join-Path $logs 'snapshot-errors.log') }
        catch { Write-Warning "Runner snapshot could not be recorded: $_" }
    }
}

$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
$start.UseShellExecute = $false
$start.WorkingDirectory = $repository
$arguments = @('test', 'Sources/HtmlTinkerX.Tests/HtmlTinkerX.Tests.csproj', '--configuration', $Configuration,
    '--no-build', '--framework', $Framework, '--verbosity', $Verbosity, '--logger', "console;verbosity=$Verbosity",
    '--logger', 'trx', '--results-directory', $results, '--blame-hang-timeout', "$($HangTimeout.TotalSeconds)s",
    '--blame-hang-dump-type', 'full', '--diag', (Join-Path $logs "vstest-$Framework.log"))
if ($Framework -eq 'net8.0') { $arguments += @('--collect', 'XPlat Code Coverage') }
if ($Filter) { $arguments += @('--filter', $Filter) }
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$runner = [Diagnostics.Process]::Start($start)
try {
    do {
        Write-RunnerSnapshot $runner
    } while (-not $runner.WaitForExit(5000))
    $runner.WaitForExit()
    $testExitCode = $runner.ExitCode
    Write-RunnerSnapshot $runner
} finally {
    # Only this exact child tree belongs to this invocation.
    if (-not $runner.HasExited) { $runner.Kill($true); $runner.WaitForExit() }
    $runner.Dispose()
}
exit $testExitCode
