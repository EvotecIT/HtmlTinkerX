Describe 'Save-HTMLAttachment streaming' {
    It 'Outputs file paths as downloads complete' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        . (Join-Path $PSScriptRoot '_TestUtils.ps1')
        $address = '127.0.0.1'
        $port = Get-FreeTcpPort -Address $address
        # Bind explicitly to IPv4 to avoid macOS IPv6-only listener issues
        $server = Start-Process -FilePath python3 -ArgumentList '-u', '-m', 'http.server', "$port", '--bind', $address -WorkingDirectory (Join-Path $PSScriptRoot 'Documents') -PassThru
        Start-Sleep -Seconds 1
        $timeout = [System.Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                $socket = New-Object Net.Sockets.TcpClient
                # Probe the exact IPv4 address and randomized port we bound to
                $socket.Connect($address, $port)
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
            $uri = "http://$address:$port/multi_download.html"
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
