Describe 'Measure-HtmlBrowserPerformance' {
    BeforeAll {
        $script:UsingLocalServer = $false
        $script:BaseUrl = $null
        if (Get-Command python3 -ErrorAction SilentlyContinue) {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start(); $port = ($listener.LocalEndpoint).Port; $listener.Stop()
            $script:Server = Start-Process -FilePath python3 -ArgumentList '-u','-m','http.server',$port,'--bind','127.0.0.1' -WorkingDirectory $PSScriptRoot -PassThru
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while ($true) { try { $c=[Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1',$port); $c.Dispose(); break } catch { if ($sw.Elapsed.TotalSeconds -gt 20) { throw 'HTTP server failed to start.' } Start-Sleep -Milliseconds 200 } }
            $script:UsingLocalServer = $true
            $script:BaseUrl = "http://127.0.0.1:$port"
        }
    }
    AfterAll {
        if ($script:Server -and -not $script:Server.HasExited) { $script:Server | Stop-Process -Force }
    }

    It 'Returns performance metrics' {
        $url = $UsingLocalServer ? ($BaseUrl + '/Documents/sample_resources.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
        $metrics = Measure-HtmlBrowserPerformance -Url $url

        $metrics | Should -Not -BeNullOrEmpty
        $metrics.TotalRequests | Should -BeGreaterOrEqual 0
        $metrics.TotalLoadTime | Should -Not -BeNullOrEmpty
    }
}
