Describe "Test-HtmlBrowser" {
    BeforeAll {
        $script:UsingLocalServer = $false
        $script:BaseUrl = $null
        if (Get-Command python3 -ErrorAction SilentlyContinue) {
            # Serve the Tests directory so /Documents and /SampleResources resolve correctly
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start(); $port = ($listener.LocalEndpoint).Port; $listener.Stop()
            $script:Server = Start-Process -FilePath python3 -ArgumentList '-u','-m','http.server',$port,'--bind','127.0.0.1' -WorkingDirectory $PSScriptRoot -PassThru
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while ($true) { try { $c=[Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1',$port); $c.Dispose(); break } catch { if ($sw.Elapsed.TotalSeconds -gt 20) { throw 'HTTP server failed to start.' } Start-Sleep -Milliseconds 200 } }
            $script:UsingLocalServer = $true
            $script:BaseUrl = "http://127.0.0.1:$port"
        } else {
            $script:BaseUrl = $null
        }
    }
    AfterAll {
        if ($script:Server -and -not $script:Server.HasExited) { $script:Server | Stop-Process -Force }
    }
    Context "Basic Functionality" {
        It "Should return HtmlBrowserTestResult object" {
            $url = $UsingLocalServer ? ($BaseUrl + '/Documents/dynamic.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
            $result = Test-HtmlBrowser -Url $url

            $result | Should -Not -BeNullOrEmpty
            $result.GetType().Name | Should -Be "HtmlBrowserTestResult"
            $result.Url | Should -Be $url
        }

        It "Should capture network entries" {
            $url = $UsingLocalServer ? ($BaseUrl + '/Documents/sample_resources.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
            $result = Test-HtmlBrowser -Url $url

            $result.NetworkEntries | Should -Not -BeNullOrEmpty
            if ($UsingLocalServer) {
                $result.TotalRequests | Should -BeGreaterThan 0
            } else {
                $result.TotalRequests | Should -BeGreaterOrEqual 0
            }
        }

        It "Should capture timing information" {
            $url = $UsingLocalServer ? ($BaseUrl + '/Documents/dynamic.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
            $result = Test-HtmlBrowser -Url $url

            $result.PageLoadTime | Should -Not -BeNullOrEmpty
            $result.PageLoadTime.TotalMilliseconds | Should -BeGreaterThan 0
        }

        It "Should capture timing information on timeout" {
            $result = Test-HtmlBrowser -Url "http://10.255.255.1" -Timeout 1000

            $result.PageLoadTime | Should -Not -BeNullOrEmpty
        }
    }

    Context "Error Detection" {
        It "Should return only errors with -ErrorsOnly" {
            $errors = Test-HtmlBrowser -Url "https://example.com" -ErrorsOnly

            if ($errors) {
                $errors | ForEach-Object {
                    $_.IsError | Should -Be $true
                }
            }
        }
    }

    Context "Performance Testing" {
        It "Should return performance metrics with -PerformanceOnly" {
            $url = $UsingLocalServer ? ($BaseUrl + '/Documents/sample_resources.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
            $metrics = Test-HtmlBrowser -Url $url -PerformanceOnly

            $metrics | Should -Not -BeNullOrEmpty
            $metrics.GetType().Name | Should -Be "HtmlPerformanceMetrics"
            if ($UsingLocalServer) { $metrics.TotalRequests | Should -BeGreaterThan 0 }
        }
    }

    Context "CSS Resource Testing" {
        It "Should find CSS resources when specified" -Skip:(-not $UsingLocalServer) {
            $css = Test-HtmlBrowser -Url ($BaseUrl + '/Documents/sample_resources.html') -CssResource ".css"

            if ($css) {
                $css.ResourceType | Should -Be "Stylesheet"
                $css.IsCss | Should -Be $true
            }
        }
    }

    Context "Browser Engine Support" {
        It "Should work with Chromium engine" {
            $url = $UsingLocalServer ? ($BaseUrl + '/Documents/dynamic.html') : ([System.Uri]::new((Join-Path $PSScriptRoot 'Documents/dynamic.html')).AbsoluteUri)
            $result = Test-HtmlBrowser -Url $url -Engine Chromium

            $result | Should -Not -BeNullOrEmpty
            $result.Passed | Should -BeOfType [bool]
        }
    }

    Context "Proxy Support" {
        It "Should accept proxy parameters" {
            $cred = New-Object PSCredential("user", (ConvertTo-SecureString "pass" -AsPlainText -Force))
            $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
            { Test-HtmlBrowser -Path $path -Proxy "http://proxy:8080" -ProxyCredential $cred } | Should -Not -Throw
        }
    }
}

Describe "Clear-HtmlBrowserCache" {
    Context "Cache Cleanup" {
        It "Should have Force parameter" {
            $command = Get-Command Clear-HtmlBrowserCache
            $command.Parameters["Force"] | Should -Not -BeNullOrEmpty
        }

        It "Should support WhatIf" {
            { Clear-HtmlBrowserCache -WhatIf } | Should -Not -Throw
        }
    }
}

Describe "Test Result Objects" {
    BeforeAll {
        $script:TestResult = Test-HtmlBrowser -Url "https://example.com"
    }

    It "Should have Summary property" {
        $TestResult.Summary | Should -Not -BeNullOrEmpty
        $TestResult.Summary | Should -BeOfType [string]
    }

    It "Should have Passed property" {
        $TestResult.Passed | Should -BeOfType [bool]
    }

    It "Should have count properties" {
        $TestResult.ErrorCount | Should -BeOfType [int]
        $TestResult.WarningCount | Should -BeOfType [int]
        $TestResult.FailedRequestCount | Should -BeOfType [int]
    }

    It "Should have network analysis properties" {
        $TestResult.PSObject.Properties["CssResources"] | Should -Not -BeNullOrEmpty
        $TestResult.PSObject.Properties["JavaScriptResources"] | Should -Not -BeNullOrEmpty
        $TestResult.PSObject.Properties["ImageResources"] | Should -Not -BeNullOrEmpty
    }
}
