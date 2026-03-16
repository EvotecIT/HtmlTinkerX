$script:Python3Available = $false
$pythonCommand = Get-Command python3 -ErrorAction SilentlyContinue
if ($pythonCommand) {
    try {
        & python3 --version *> $null
        if ($LASTEXITCODE -eq 0) {
            $script:Python3Available = $true
        }
    } catch {
        $script:Python3Available = $false
    }
}

Describe 'Save-HTMLAttachment' {
    It 'Saves downloads on the page by filter' -Skip:(-not $script:Python3Available) {
        $server = Start-Process -FilePath 'python3' -ArgumentList '-u', '-m', 'http.server', '8011', '--bind', '127.0.0.1' -WorkingDirectory (Join-Path $PSScriptRoot 'Documents') -PassThru
        Start-Sleep -Seconds 1
        $timeout = [System.Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                $socket = New-Object Net.Sockets.TcpClient
                $socket.Connect('127.0.0.1', 8011)
                $socket.Dispose()
                break
            } catch {
                if ($timeout.Elapsed -gt [TimeSpan]::FromSeconds(10)) {
                    throw 'HTTP server failed to start.'
                }
                Start-Sleep -Milliseconds 500
            }
        }

        try {
            $dest = Join-Path $TestDrive 'dl'
            [array] $files = Save-HTMLAttachment -Url 'http://127.0.0.1:8011/multi_manual_download.html' -Path $dest -Filter 'download'
            $files.Count | Should -Be 2
            (Get-Item -Path $files[0]).Name | Should -BeIn @('download1.txt', 'download2.txt')
            (Get-Item -Path $files[1]).Name | Should -BeIn @('download1.txt', 'download2.txt')
        } finally {
            $server | Stop-Process
        }
    }

    It 'Downloads are fully written to disk' -Skip:(-not $script:Python3Available) {
        $server = Start-Process -FilePath 'python3' -ArgumentList '-u', '-m', 'http.server', '8012', '--bind', '127.0.0.1' -WorkingDirectory (Join-Path $PSScriptRoot 'Documents') -PassThru
        Start-Sleep -Seconds 1
        $timeout = [System.Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                $socket = New-Object Net.Sockets.TcpClient
                $socket.Connect('127.0.0.1', 8012)
                $socket.Dispose()
                break
            } catch {
                if ($timeout.Elapsed -gt [TimeSpan]::FromSeconds(10)) {
                    throw 'HTTP server failed to start.'
                }
                Start-Sleep -Milliseconds 500
            }
        }

        try {
            $dest = Join-Path $TestDrive 'dl-full'
            [array] $files = Save-HTMLAttachment -Url 'http://127.0.0.1:8012/multi_manual_download.html' -Path $dest -Filter 'download'
            foreach ($path in $files) {
                (Get-Item $path).Length | Should -BeGreaterThan 0
            }
        } finally {
            $server | Stop-Process
        }
    }
}
