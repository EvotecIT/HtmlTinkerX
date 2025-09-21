Describe 'Save-HTMLAttachment streaming' {
    It 'Outputs file paths as downloads complete' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        # Bind explicitly to IPv4 to avoid macOS IPv6-only listener issues
        $server = Start-Process -FilePath python3 -ArgumentList '-u', '-m', 'http.server', '8010', '--bind', '127.0.0.1' -WorkingDirectory (Join-Path $PSScriptRoot 'Documents') -PassThru
        Start-Sleep -Seconds 1
        $timeout = [System.Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                $socket = New-Object Net.Sockets.TcpClient
                # Use localhost to allow IPv4/IPv6 resolution as needed
                $socket.Connect('localhost', 8010)
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
            # Match the server bind address to avoid IPv6/IPv4 mismatch
            $uri = 'http://127.0.0.1:8010/multi_download.html'
            $dest = Join-Path $TestDrive 'stream'
            $results = @()
            foreach ($file in Save-HTMLAttachment -Url $uri -Path $dest) {
                $results += $file
            }
            $results.Count | Should -Be 2
            Test-Path (Join-Path $dest 'download1.txt') | Should -BeTrue
            Test-Path (Join-Path $dest 'download2.txt') | Should -BeTrue
        }
        finally {
            $server | Stop-Process
        }
    }
}
