Describe "Test-HtmlBrowser" {
    Context "Basic Functionality" {
        It "Should return HtmlBrowserTestResult object" {
            $result = Test-HtmlBrowser -Url "https://example.com"

            $result | Should -Not -BeNullOrEmpty
            $result.GetType().Name | Should -Be "HtmlBrowserTestResult"
            $result.Url | Should -Be "https://example.com"
        }

        It "Should capture network entries" {
            $result = Test-HtmlBrowser -Url "https://example.com"

            $result.NetworkEntries | Should -Not -BeNullOrEmpty
            $result.TotalRequests | Should -BeGreaterThan 0
        }

        It "Should capture timing information" {
            $result = Test-HtmlBrowser -Url "https://example.com"

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
            $metrics = Test-HtmlBrowser -Url "https://example.com" -PerformanceOnly

            $metrics | Should -Not -BeNullOrEmpty
            $metrics.GetType().Name | Should -Be "HtmlPerformanceMetrics"
            $metrics.TotalRequests | Should -BeGreaterThan 0
        }
    }

    Context "CSS Resource Testing" {
        It "Should find CSS resources when specified" {
            $css = Test-HtmlBrowser -Url "https://example.com" -CssResource ".css"

            if ($css) {
                $css.ResourceType | Should -Be "Stylesheet"
                $css.IsCss | Should -Be $true
            }
        }
    }

    Context "Browser Engine Support" {
        It "Should work with Chromium engine" {
            $result = Test-HtmlBrowser -Url "https://example.com" -Engine Chromium

            $result | Should -Not -BeNullOrEmpty
            $result.Passed | Should -BeOfType [bool]
        }
    }

    Context "Proxy Support" {
        It "Should accept proxy parameters" {
            $cred = New-Object PSCredential("user", (ConvertTo-SecureString "pass" -AsPlainText -Force))

            { Test-HtmlBrowser -Url "https://example.com" -Proxy "http://proxy:8080" -ProxyCredential $cred } |
                Should -Not -Throw
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