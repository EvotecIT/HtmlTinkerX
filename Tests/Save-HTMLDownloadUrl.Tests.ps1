Describe 'Save-HTMLAttachment' {
    It 'Saves downloads on the page by filter' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        # Arrange a local site with two zip assets
        $root = Join-Path $TestDrive 'site'
        New-Item -ItemType Directory -Path $root | Out-Null
        $c1 = Join-Path $root 'c1'; New-Item -ItemType Directory -Path $c1 | Out-Null
        $c2 = Join-Path $root 'c2'; New-Item -ItemType Directory -Path $c2 | Out-Null
        Set-Content -Path (Join-Path $c1 'a.txt') -Value 'one'
        Set-Content -Path (Join-Path $c2 'b.txt') -Value 'two'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip1 = Join-Path $root 'DnsClientX-PowerShellModule.v0.4.0.zip'
        $zip2 = Join-Path $root 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
        [IO.Compression.ZipFile]::CreateFromDirectory($c1, $zip1)
        [IO.Compression.ZipFile]::CreateFromDirectory($c2, $zip2)
        $html = @"
<!DOCTYPE html>
<html><body>
  <a href="DnsClientX-PowerShellModule.v0.4.0.zip">asset1</a>
  <a href="DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip">asset2</a>
  <script>document.addEventListener('DOMContentLoaded',()=>window.scrollTo(0,document.body.scrollHeight));</script>
  </body></html>
"@
        Set-Content -Encoding utf8 -Path (Join-Path $root 'links.html') -Value $html

        # Start a local server bound to IPv4 on a free port
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0); $listener.Start(); $port = ($listener.LocalEndpoint).Port; $listener.Stop()
        $server = Start-Process -FilePath python3 -ArgumentList '-u','-m','http.server',$port,'--bind','127.0.0.1' -WorkingDirectory $root -PassThru
        try {
            $sw = [Diagnostics.Stopwatch]::StartNew();
            while ($true) { try { $c=[Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1',$port); $c.Dispose(); break } catch { if ($sw.Elapsed.TotalSeconds -gt 15) { throw 'HTTP server failed to start.' } Start-Sleep -Milliseconds 200 } }

            # Act
            $dest = Join-Path $TestDrive 'dl'
            [Array] $files = Save-HTMLAttachment -Url "http://127.0.0.1:$port/links.html" -Path $dest -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'

            # Assert
            $files.Count | Should -Be 2
            $names = $files | ForEach-Object { (Get-Item $_).Name }
            $names | Should -Contain 'DnsClientX-PowerShellModule.v0.4.0.zip'
            $names | Should -Contain 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'
        } finally {
            if ($server -and -not $server.HasExited) { $server | Stop-Process -Force }
        }
    }

    It 'Downloads are fully written to disk' -Skip:(-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        # Reuse the same flow, verifying sizes > 0
        $root = Join-Path $TestDrive 'site2'
        New-Item -ItemType Directory -Path $root | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        1..2 | ForEach-Object {
            $dir = Join-Path $root "c$_"; New-Item -ItemType Directory -Path $dir | Out-Null; Set-Content -Path (Join-Path $dir "f$_`n.txt") -Value "data$_"
        }
        [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'c1'), (Join-Path $root 'DnsClientX-PowerShellModule.v0.4.0.zip'))
        [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'c2'), (Join-Path $root 'DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip'))
        Set-Content -Encoding utf8 -Path (Join-Path $root 'links.html') -Value '<a href="DnsClientX-PowerShellModule.v0.4.0.zip">a</a><a href="DnsClientX-DnsClientX-PowerShellModule.v0.4.0.zip">b</a>'
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0); $listener.Start(); $port = ($listener.LocalEndpoint).Port; $listener.Stop()
        $server = Start-Process -FilePath python3 -ArgumentList '-u','-m','http.server',$port,'--bind','127.0.0.1' -WorkingDirectory $root -PassThru
        try {
            $sw = [Diagnostics.Stopwatch]::StartNew();
            while ($true) { try { $c=[Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1',$port); $c.Dispose(); break } catch { if ($sw.Elapsed.TotalSeconds -gt 15) { throw 'HTTP server failed to start.' } Start-Sleep -Milliseconds 200 } }

            $dest = Join-Path $TestDrive 'dl-full'
            [array] $files = Save-HTMLAttachment -Url "http://127.0.0.1:$port/links.html" -Path $dest -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
            foreach ($p in $files) {
                (Get-Item $p).Length | Should -BeGreaterThan 0
            }
        } finally {
            if ($server -and -not $server.HasExited) { $server | Stop-Process -Force }
        }
    }
}
