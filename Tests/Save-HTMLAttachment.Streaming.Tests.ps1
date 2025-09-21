Describe 'Save-HTMLAttachment streaming' {
    It 'Outputs file paths as downloads complete' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        # Pick an ephemeral free port on IPv4 loopback to avoid collisions and IPv6-only binds
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $port = ($listener.LocalEndpoint).Port
        $listener.Stop()

        $docs = Join-Path $PSScriptRoot 'Documents'
        $args = @('-u','-m','http.server', $port, '--bind', '127.0.0.1')
        $server = Start-Process -FilePath python3 -ArgumentList $args -WorkingDirectory $docs -PassThru

        # Wait until the server is accepting connections (robust on macOS/IPv6)
        $timeout = [System.Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                $socket = [System.Net.Sockets.TcpClient]::new()
                $socket.Connect('127.0.0.1', $port)
                $socket.Dispose()
                break
            } catch {
                if ($timeout.Elapsed -gt [TimeSpan]::FromSeconds(20)) {
                    throw 'HTTP server failed to start.'
                }
                Start-Sleep -Milliseconds 250
            }
        }

        try {
            $uri = "http://127.0.0.1:$port/multi_download.html"
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
            if ($server -and -not $server.HasExited) { $server | Stop-Process -Force }
        }
    }
}
