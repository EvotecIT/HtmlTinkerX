# Pester tests for browser testing with PSParseHTML

Describe "Browser Testing Examples" {
    BeforeAll {
        Import-Module PSParseHTML -Force
        $Script:TestUrl = "https://example.com"
    }

    Context "Console Error Testing" {
        It "Should have no console errors" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # Check for errors
            $result.ErrorCount | Should -Be 0
            $result.Passed | Should -Be $true
        }

        It "Should have no console warnings" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # Check for warnings
            $result.WarningCount | Should -Be 0
        }

        It "Should capture console error details when present" {
            # Test a page known to have errors (hypothetical)
            $result = Test-HtmlBrowser -Url "https://example.com/page-with-errors"
            
            if ($result.ErrorCount -gt 0) {
                $firstError = $result.ConsoleErrors | Select-Object -First 1
                
                # Verify error details
                $firstError.Text | Should -Not -BeNullOrEmpty
                $firstError.Type | Should -Be "Error"
                $firstError.Timestamp | Should -Not -BeNullOrEmpty
                $firstError.SeverityLevel | Should -Be 3
            }
        }
    }

    Context "Network Request Testing" {
        It "Should have no failed network requests" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # Check for failed requests
            $result.FailedRequestCount | Should -Be 0
            
            # Verify all requests succeeded
            $result.NetworkEntries | ForEach-Object {
                $_.Status | Should -Not -BeNullOrEmpty
                [int]$_.Status | Should -BeLessThan 400
            }
        }

        It "Should load CSS resources" {
            # Test for CSS resources
            $result = Test-HtmlBrowser -Url $TestUrl
            $cssResources = $result.NetworkEntries | Where-Object { $_.IsCss }
            
            # Should have at least one CSS file
            $cssResources.Count | Should -BeGreaterThan 0
        }

        It "Should find specific CSS file" {
            # Test for specific CSS
            $cssResource = Test-HtmlBrowser -Url $TestUrl -CssResource "styles.css"
            
            if ($cssResource) {
                $cssResource.Url | Should -Match "styles\.css"
                $cssResource.ResourceType | Should -Be "Stylesheet"
                $cssResource.Duration | Should -Not -BeNullOrEmpty
            }
        }

        It "Should track resource load times" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # Check CSS load times
            $cssResources = $result.NetworkEntries | Where-Object { $_.IsCss }
            
            $cssResources | ForEach-Object {
                $_.Duration | Should -Not -BeNullOrEmpty
                $_.Duration.TotalMilliseconds | Should -BeLessThan 2000  # Max 2 seconds
            }
        }
    }

    Context "Performance Testing" {
        It "Should load page within acceptable time" {
            # Get performance metrics
            $metrics = Test-HtmlBrowser -Url $TestUrl -PerformanceOnly
            
            # Check load time
            $metrics.TotalLoadTime | Should -Not -BeNullOrEmpty
            $metrics.TotalLoadTime.TotalSeconds | Should -BeLessThan 5
        }

        It "Should not make excessive requests" {
            # Get performance metrics
            $metrics = Test-HtmlBrowser -Url $TestUrl -PerformanceOnly
            
            # Check request count
            $metrics.TotalRequests | Should -BeLessThan 50
        }

        It "Should have reasonable average request duration" {
            # Get performance metrics
            $metrics = Test-HtmlBrowser -Url $TestUrl -PerformanceOnly
            
            # Check average duration
            $metrics.AverageRequestDuration.TotalMilliseconds | Should -BeLessThan 500
        }
    }

    Context "Cross-Browser Testing" {
        It "Should work in Chromium" {
            $result = Test-HtmlBrowser -Url $TestUrl -Engine Chromium
            $result.Passed | Should -Be $true
        }

        It "Should work in Firefox" -Skip:(-not (Test-Path "$env:LOCALAPPDATA\ms-playwright\firefox*")) {
            $result = Test-HtmlBrowser -Url $TestUrl -Engine Firefox
            $result.Passed | Should -Be $true
        }

        It "Should work in WebKit" -Skip:(-not (Test-Path "$env:LOCALAPPDATA\ms-playwright\webkit*")) {
            $result = Test-HtmlBrowser -Url $TestUrl -Engine WebKit
            $result.Passed | Should -Be $true
        }
    }

    Context "Error Detection Examples" {
        It "Should detect JavaScript errors" {
            # Get only errors
            $errors = Test-HtmlBrowser -Url $TestUrl -ErrorsOnly
            
            # Check each error
            $errors | ForEach-Object {
                $_.IsError | Should -Be $true
                $_.Text | Should -Not -BeNullOrEmpty
            }
        }

        It "Should provide error location information" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # If there are errors, check their details
            $result.ConsoleErrors | ForEach-Object {
                if ($_.SourceUrl) {
                    $_.SourceUrl | Should -Not -BeNullOrEmpty
                    $_.FullLocation | Should -Not -BeNullOrEmpty
                }
            }
        }
    }

    Context "Resource Type Testing" {
        It "Should correctly identify resource types" {
            # Test the page
            $result = Test-HtmlBrowser -Url $TestUrl
            
            # Group by resource type
            $resourceTypes = $result.NetworkEntries | Group-Object ResourceType
            
            # Display breakdown
            Write-Host "`nResource Breakdown:"
            $resourceTypes | ForEach-Object {
                Write-Host "  $($_.Name): $($_.Count) requests"
            }
            
            # Should have at least documents and stylesheets
            $resourceTypes.Name | Should -Contain "Document"
        }
    }
}

Describe "Browser Cache Management" {
    It "Should clear Playwright cache" {
        # Clear cache
        Clear-HtmlBrowserCache -Force
        
        # Verify cache directory
        $cacheDir = "$env:LOCALAPPDATA\ms-playwright"
        Test-Path $cacheDir | Should -Be $false
    }
}