# Test pages and inspect browser network activity

[Back to the project overview](../README.MD)

## 🧪 Browser Testing & Network Monitoring

PSParseHTML now includes comprehensive browser testing capabilities for checking network requests, CSS resources, console errors, and performance metrics. This feature uses strongly-typed classes instead of dictionaries for better IntelliSense and type safety.

### PowerShell Browser Testing

#### Basic Testing
```powershell
# Run a comprehensive test on a URL
$result = Test-HtmlBrowser -Url 'https://example.com'

# Test a local HTML file
$result = Test-HtmlBrowser -Path 'C:\MyProject\index.html'

# Check if test passed (no errors or failed requests)
if ($result.Passed) {
    Write-Host "✅ All tests passed!"
} else {
    Write-Host "❌ Issues found: $($result.Summary)"
}

# View detailed results
Write-Host "Total Requests: $($result.TotalRequests)"
Write-Host "Failed Requests: $($result.FailedRequestCount)"
Write-Host "Console Errors: $($result.ErrorCount)"
Write-Host "Console Warnings: $($result.WarningCount)"
```

#### Testing Local HTML Files
```powershell
# Test local HTML files created by HTMLForgeX or other tools
$htmlFile = "C:\Projects\MyReport\report.html"
$result = Test-HtmlBrowser -Path $htmlFile

# Check for JavaScript errors in local file
$errors = Test-HtmlBrowser -Path $htmlFile -ErrorsOnly
if ($errors.Count -gt 0) {
    Write-Host "Found $($errors.Count) JavaScript errors:"
    $errors | ForEach-Object {
        Write-Host "  - $($_.Text) at $($_.FullLocation)"
    }
}

# Test CSS loading in local file
$cssCheck = Test-HtmlBrowser -Path $htmlFile -CssResource 'styles.css'
if ($cssCheck) {
    Write-Host "CSS loaded successfully in $($cssCheck.Duration.TotalMilliseconds)ms"
}

# Test with visible browser (not headless) for debugging
$result = Test-HtmlBrowser -Path $htmlFile -Headless:$false
```

#### Testing for Console Errors
```powershell
# Get only console errors
$errors = Test-HtmlBrowser -Url 'https://example.com' -ErrorsOnly

foreach ($error in $errors) {
    Write-Host "Error: $($error.Text)"
    Write-Host "  Location: $($error.FullLocation)"
    Write-Host "  Severity: $($error.SeverityLevel)"

    if ($error.StackTrace) {
        Write-Host "  Stack: $($error.StackTrace)"
    }
}
```

#### CSS Resource Testing
```powershell
# Check if a specific CSS file is loaded
$cssResource = Test-HtmlBrowser -Url 'https://example.com' -CssResource 'styles.css'

if ($cssResource) {
    Write-Host "CSS found: $($cssResource.Url)"
    Write-Host "Load time: $($cssResource.Duration.TotalMilliseconds)ms"
    Write-Host "Size: $($cssResource.TransferSize) bytes"
    Write-Host "From cache: $($cssResource.ServedFromCache)"
}
```

#### Performance Testing
```powershell
# Get performance metrics only
$metrics = Test-HtmlBrowser -Url 'https://example.com' -PerformanceOnly

# Display performance report
Write-Host $metrics.GetReport()

# Access specific metrics
Write-Host "Page Load Time: $($metrics.TotalLoadTime.TotalSeconds)s"
Write-Host "Average Request Duration: $($metrics.AverageRequestDuration.TotalMilliseconds)ms"
Write-Host "Total Bytes: $($metrics.TotalBytesTransferred / 1KB)KB"

# Resource breakdown by type
$metrics.ResourceBreakdown | ForEach-Object {
    Write-Host "$($_.Key): $($_.Value) requests"
}

# Or get the full formatted report
Write-Host $metrics.GetReport()
```

#### Advanced Testing with Proxy
```powershell
# Test through a proxy with authentication
$cred = Get-Credential
$result = Test-HtmlBrowser -Url 'https://example.com' `
    -Proxy 'http://proxy:8080' `
    -ProxyCredential $cred `
    -Timeout 60000
```

#### Batch Testing Multiple URLs
```powershell
# Test multiple URLs and generate report
$urls = @(
    'https://example.com/home',
    'https://example.com/about',
    'https://example.com/contact'
)

$results = $urls | ForEach-Object {
    $result = Test-HtmlBrowser -Url $_
    [PSCustomObject]@{
        Url = $_
        Status = if ($result.Passed) { 'PASS' } else { 'FAIL' }
        LoadTime = $result.PageLoadTime.TotalSeconds
        Requests = $result.TotalRequests
        Failed = $result.FailedRequestCount
        Errors = $result.ErrorCount
        Warnings = $result.WarningCount
    }
}

# Display results in a table
$results | Format-Table -AutoSize

# Export to CSV for further analysis
$results | Export-Csv -Path 'browser-test-results.csv' -NoTypeInformation

# Find pages with issues
$results | Where-Object { $_.Status -eq 'FAIL' } | ForEach-Object {
    Write-Warning "Failed: $($_.Url) - $($_.Failed) failed requests, $($_.Errors) errors"
}
```

#### Integration with Pester Tests
```powershell
# Save as MyWebsite.Tests.ps1
Describe "Website Browser Tests" {

    BeforeAll {
        $baseUrl = 'https://mywebsite.com'
    }

    It "Homepage should load without errors" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $result.Passed | Should -BeTrue
        $result.ConsoleErrors.Count | Should -Be 0
        $result.FailedRequestCount | Should -Be 0
    }

    It "All CSS files should load successfully" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $cssFiles = $result.CssResources

        $cssFiles.Count | Should -BeGreaterThan 0
        $cssFiles | ForEach-Object {
            $_.Status | Should -Be 200
            $_.ErrorType | Should -BeNullOrEmpty
        }
    }

    It "Page should load within 3 seconds" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $result.PageLoadTime.TotalSeconds | Should -BeLessOrEqual 3
    }

    It "Console should not contain JavaScript errors" {
        $errors = Test-HtmlBrowser -Url $baseUrl -ErrorsOnly
        $errors | Should -BeNullOrEmpty
    }

    It "Total page size should be under 5MB" {
        $metrics = Test-HtmlBrowser -Url $baseUrl -PerformanceOnly
        $totalMB = $metrics.TotalBytesTransferred / 1MB
        $totalMB | Should -BeLessOrEqual 5
    }
}

# Run tests
Invoke-Pester -Path .\MyWebsite.Tests.ps1 -Output Detailed
```

#### Testing Local HTML Reports
```powershell
# Test HTMLForgeX generated reports
$reportPath = "C:\Reports\MonthlyReport.html"

# Basic test
$result = Test-HtmlBrowser -Path $reportPath
if (-not $result.Passed) {
    Write-Warning "Report has issues:"
    $result.ConsoleErrors | ForEach-Object {
        Write-Warning "  JS Error: $($_.Text)"
    }
    $result.FailedRequests | ForEach-Object {
        Write-Warning "  Failed Resource: $($_.Url)"
    }
}

# Test multiple reports
Get-ChildItem -Path "C:\Reports" -Filter "*.html" | ForEach-Object {
    $result = Test-HtmlBrowser -Path $_.FullName
    [PSCustomObject]@{
        Report = $_.Name
        Status = if ($result.Passed) { '✅' } else { '❌' }
        LoadTime = "$($result.PageLoadTime.TotalSeconds)s"
        Errors = $result.ErrorCount
        MissingResources = $result.FailedRequestCount
    }
} | Format-Table -AutoSize

# Test with visible browser for debugging
$debugResult = Test-HtmlBrowser -Path $reportPath -Headless:$false -Timeout 60000
```

#### Monitoring and Alerting
```powershell
# Monitor website health
function Test-WebsiteHealth {
    param(
        [string]$Url,
        [int]$MaxLoadTime = 5,
        [int]$MaxErrors = 0
    )

    $result = Test-HtmlBrowser -Url $Url

    $issues = @()

    if ($result.PageLoadTime.TotalSeconds -gt $MaxLoadTime) {
        $issues += "Slow load time: $($result.PageLoadTime.TotalSeconds)s"
    }

    if ($result.ErrorCount -gt $MaxErrors) {
        $issues += "Console errors: $($result.ErrorCount)"
    }

    if ($result.FailedRequestCount -gt 0) {
        $issues += "Failed requests: $($result.FailedRequestCount)"
    }

    if ($issues.Count -eq 0) {
        Write-Host "✅ $Url is healthy" -ForegroundColor Green
    } else {
        Write-Host "❌ $Url has issues:" -ForegroundColor Red
        $issues | ForEach-Object { Write-Host "   - $_" -ForegroundColor Yellow }

        # Send alert (example)
        # Send-MailMessage -To "admin@company.com" -Subject "Website Issue" -Body ($issues -join "`n")
    }

    return @{
        Url = $Url
        Healthy = $issues.Count -eq 0
        Issues = $issues
        Timestamp = Get-Date
    }
}

# Test multiple sites
$sites = @('https://site1.com', 'https://site2.com')
$healthChecks = $sites | ForEach-Object { Test-WebsiteHealth -Url $_ }

# Save results
$healthChecks | ConvertTo-Json | Out-File "health-check-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
```

### C# Browser Testing

#### Basic Testing
```csharp
using HtmlTinkerX;

// Run comprehensive test on URL
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Test a local HTML file
var fileResult = await HtmlBrowserTester.TestFileAsync(@"C:\MyProject\index.html");

if (result.Passed)
{
    Console.WriteLine("✅ All tests passed!");
}
else
{
    Console.WriteLine($"❌ {result.Summary}");
}

// Analyze results
Console.WriteLine($"Total Requests: {result.TotalRequests}");
Console.WriteLine($"Failed: {result.FailedRequestCount}");
Console.WriteLine($"Errors: {result.ErrorCount}");
Console.WriteLine($"Warnings: {result.WarningCount}");
```

#### Testing Local HTML Files
```csharp
// Test local HTML file with full analysis
var testResult = await HtmlBrowserTester.TestFileAsync(
    @"C:\Projects\MyReport\report.html",
    HtmlBrowserEngine.Chromium,
    headless: true,
    timeout: 30000);

// Check specific issues
if (testResult.ConsoleErrors.Any())
{
    Console.WriteLine($"Found {testResult.ErrorCount} JavaScript errors:");
    foreach (var error in testResult.ConsoleErrors)
    {
        Console.WriteLine($"  - {error.Text}");
        Console.WriteLine($"    Location: {error.FullLocation}");
        if (!string.IsNullOrEmpty(error.StackTrace))
        {
            Console.WriteLine($"    Stack: {error.StackTrace}");
        }
    }
}

// Analyze resource loading
var slowResources = testResult.NetworkEntries
    .Where(r => r.Duration > TimeSpan.FromSeconds(1))
    .OrderByDescending(r => r.Duration);

foreach (var resource in slowResources)
{
    Console.WriteLine($"Slow resource: {resource.Url} took {resource.Duration?.TotalSeconds}s");
}
```

#### Network Request Analysis
```csharp
// Test and analyze network requests
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Check CSS resources
foreach (var css in result.CssResources)
{
    Console.WriteLine($"CSS: {css.Url}");
    Console.WriteLine($"  Duration: {css.Duration?.TotalMilliseconds}ms");
    Console.WriteLine($"  Size: {css.TransferSize} bytes");
    Console.WriteLine($"  Cached: {css.ServedFromCache}");
}

// Check failed requests
foreach (var failed in result.FailedRequests)
{
    Console.WriteLine($"Failed: {failed.Url}");
    Console.WriteLine($"  Error: {failed.ErrorType} - {failed.ErrorMessage}");
}

// Check JavaScript resources
var jsFiles = result.JavaScriptResources;
var totalJsSize = jsFiles.Sum(js => js.TransferSize ?? 0);
Console.WriteLine($"Total JS size: {totalJsSize / 1024}KB");
```

#### Console Error Detection
```csharp
// Get only console errors
var errors = await HtmlBrowserTester.TestConsoleErrorsAsync("https://example.com");

foreach (var error in errors)
{
    Console.WriteLine($"Error: {error.Text}");
    Console.WriteLine($"  Type: {error.Type}");
    Console.WriteLine($"  Location: {error.FullLocation}");
    Console.WriteLine($"  Timestamp: {error.Timestamp}");

    if (!string.IsNullOrEmpty(error.StackTrace))
    {
        Console.WriteLine($"  Stack: {error.StackTrace}");
    }
}
```

#### Performance Analysis
```csharp
// Get performance metrics
var metrics = await HtmlBrowserTester.TestPerformanceAsync("https://example.com");

// Display performance report
Console.WriteLine(metrics.GetReport());

// Check specific thresholds
if (metrics.TotalLoadTime > TimeSpan.FromSeconds(5))
{
    Console.WriteLine("⚠️ Page load time exceeds 5 seconds!");
}

if (metrics.LongestRequest?.Duration > TimeSpan.FromSeconds(2))
{
    Console.WriteLine($"⚠️ Slow resource: {metrics.LongestRequest.Url}");
}
```

#### Testing Local HTML Files
```csharp
// Test a local HTML file created by HTMLForgeX or other tools
var localResult = await HtmlBrowserTester.TestFileAsync(@"C:\Projects\MyReport\report.html");

// Check if all resources loaded correctly
if (localResult.Passed)
{
    Console.WriteLine("✅ Local HTML file passed all tests!");
}
else
{
    // Analyze what went wrong
    foreach (var failed in localResult.FailedRequests)
    {
        Console.WriteLine($"❌ Failed to load: {failed.Url}");
        Console.WriteLine($"   Error: {failed.ErrorType}");
    }
}

// Test with custom timeout for slow local resources
var slowResult = await HtmlBrowserTester.TestFileAsync(
    @"C:\MyProject\index.html",
    timeout: 30000  // 30 seconds
);
```

#### Integration Testing Examples
```csharp
// Example: Testing in xUnit
[Fact]
public async Task Website_Should_Load_Without_Errors()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    Assert.True(result.Passed, $"Test failed: {result.Summary}");
    Assert.Empty(result.ConsoleErrors);
    Assert.Empty(result.FailedRequests);
    Assert.True(result.PageLoadTime < TimeSpan.FromSeconds(3),
        "Page load time exceeded 3 seconds");
}

// Example: Testing specific CSS resources
[Theory]
[InlineData("styles.css")]
[InlineData("theme.css")]
public async Task CSS_Files_Should_Load_Successfully(string cssFile)
{
    var css = await HtmlBrowserTester.TestCssResourceAsync(
        "https://mysite.com", cssFile);

    Assert.NotNull(css);
    Assert.Equal(200, css.Status);
    Assert.True(css.Duration < TimeSpan.FromSeconds(1));
}

// Example: Performance regression test
[Fact]
public async Task Page_Performance_Should_Meet_Thresholds()
{
    var metrics = await HtmlBrowserTester.TestPerformanceAsync("https://mysite.com");

    Assert.True(metrics.TotalLoadTime < TimeSpan.FromSeconds(5));
    Assert.True(metrics.TotalBytesTransferred < 5 * 1024 * 1024); // 5MB
    Assert.True(metrics.TotalRequests < 50);
    metrics.ResourceBreakdown.TryGetValue(
        HtmlNetworkResourceType.Image,
        out int imageRequestCount);
    Assert.True(imageRequestCount < 20,
        $"Image request count exceeds limit: {imageRequestCount}");
}
```

#### Batch Testing Multiple Pages
```csharp
// Test multiple pages efficiently
var urls = new[] {
    "https://example.com/home",
    "https://example.com/about",
    "https://example.com/contact"
};

var results = await Task.WhenAll(
    urls.Select(url => HtmlBrowserTester.TestUrlAsync(url))
);

// Generate summary report
foreach (var (url, result) in urls.Zip(results))
{
    Console.WriteLine($"\n{url}:");
    Console.WriteLine($"  Status: {(result.Passed ? "PASS" : "FAIL")}");
    Console.WriteLine($"  Load Time: {(result.PageLoadTime?.TotalSeconds ?? 0):F2}s");
    Console.WriteLine($"  Requests: {result.TotalRequests} ({result.FailedRequestCount} failed)");
    Console.WriteLine($"  Console: {result.ErrorCount} errors, {result.WarningCount} warnings");
}

// Find slowest page
var slowest = results.OrderByDescending(r => r.PageLoadTime ?? TimeSpan.Zero).First();
Console.WriteLine($"\nSlowest page: {slowest.Url} ({(slowest.PageLoadTime?.TotalSeconds ?? 0):F2}s)");
```

### Test Result Properties

#### HtmlBrowserTestResult
- **Url** - The tested URL
- **PageLoadTime** - Total page load duration
- **NetworkEntries** - All network requests with detailed info
- **ConsoleEntries** - All console messages
- **ConsoleErrors** - Only error messages
- **ConsoleWarnings** - Only warning messages
- **FailedRequests** - Failed network requests
- **CssResources** - CSS file requests
- **JavaScriptResources** - JS file requests
- **ImageResources** - Image requests
- **Passed** - Whether all tests passed
- **Summary** - Human-readable summary

#### HtmlNetworkEntryDetailed
- **Url** - Request URL
- **Method** - HTTP method
- **Status** - Response status code
- **ProtocolVersion** - HTTP protocol version
- **Duration** - Request duration
- **ResourceType** - Type of resource (Document, Stylesheet, Script, etc.)
- **TransferSize** - Total bytes transferred
- **ServedFromCache** - Whether served from cache
- **ErrorType** - Error type if failed
- **ContentType** - Response content type

#### HtmlConsoleEntryDetailed
- **Text** - Console message text
- **Type** - Message type (Error, Warning, Info, etc.)
- **Timestamp** - When logged
- **SourceUrl** - Source file URL
- **LineNumber** - Line in source
- **StackTrace** - Stack trace for errors
- **SeverityLevel** - 1=Info, 2=Warning, 3=Error
- **IsError/IsWarning/IsInfo** - Quick type checks

#### HtmlPerformanceMetrics
- **TotalLoadTime** - Total time to load the page
- **TotalRequests** - Number of network requests made
- **TotalBytesTransferred** - Total bytes downloaded
- **AverageRequestDuration** - Average time per request
- **LongestRequest** - The slowest network request
- **ResourceBreakdown** - Dictionary of requests grouped by type (Document, Stylesheet, Script, Image, Font, etc.)
- **GetReport()** - Returns a formatted text report with all metrics

### Playwright Auto-Setup

Playwright browsers are automatically downloaded on first use. No manual setup required. The download is cached per-user (default locations below).

#### How Auto-Download Works
When you first use browser testing, Playwright automatically downloads required components:

1. **Playwright Driver & Node.js**:
   - Windows: `%LOCALAPPDATA%\ms-playwright-driver`
   - macOS: `~/Library/Caches/ms-playwright-driver`
   - Linux: `~/.cache/ms-playwright-driver`
   - Contains the Playwright driver and embedded Node.js runtime

2. **Browser Installations**:
   - Windows: `%LOCALAPPDATA%\ms-playwright`
   - macOS: `~/Library/Caches/ms-playwright`
   - Linux: `~/.cache/ms-playwright`
   - Contains Chromium, Firefox, and/or WebKit browsers

3. **Download Process**:
   - Shows progress: "Downloading Playwright driver... X% (Y MB/s)"
   - Thread-safe - prevents concurrent downloads
   - Subsequent runs use cached components - no re-download needed
   - You can manually ensure Chromium is installed using `HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium)`

`PLAYWRIGHT_DRIVER_SEARCH_PATH` can point to the parent that contains `.playwright` or to the `.playwright` directory itself, with or without a trailing separator. Driver repair and cleanup remove only that `.playwright` directory and preserve the parent and its other files. Bundled driver assets are preserved during cleanup.

#### Linux: Avoiding sudo prompts
On Linux, Playwright can also install OS-level dependencies when invoked with `--with-deps` (this typically requires root/sudo).

By default, HtmlTinkerX only uses `--with-deps` when running as root to avoid unexpected sudo prompts during normal test execution. You can override this behavior by setting:
- `HTMLTINKERX_PLAYWRIGHT_WITH_DEPS=1` to force `--with-deps`
- `HTMLTINKERX_PLAYWRIGHT_WITH_DEPS=0` to never use `--with-deps`

#### Cleaning Playwright Cache
```powershell
# View cache size and clean if needed
Clear-HtmlBrowserCache -WhatIf

# Force clean without confirmation
Clear-HtmlBrowserCache -Force

# Skip cleaning temporary files (only clean browser downloads)
Clear-HtmlBrowserCache -SkipTemp -Force

# Skip cleaning browser downloads (only clean temp files)
Clear-HtmlBrowserCache -SkipBrowsers -Force

# View detailed information about what will be cleaned
Clear-HtmlBrowserCache -Verbose
```

The enhanced cache cleaner now:
- Cleans multiple Playwright cache locations (LocalAppData and .cache)
- Removes temporary Playwright files from the temp directory
- Cleans up trace files left behind by debugging sessions
- Shows detailed size information for each location
- Provides granular control over what to clean

#### C# Cache Cleaning
```csharp
// Manually ensure browser is installed (usually not needed - happens automatically)
await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

// Get all cache locations
var locations = HtmlBrowserCacheCleaner.GetCacheLocations();
Console.WriteLine($"Found {locations.Count} locations totaling {locations.Sum(l => l.SizeMB):F2} MB");

// Clean all cache
var result = HtmlBrowserCacheCleaner.CleanAllCache();
if (result.Success)
{
    Console.WriteLine($"Cleaned {result.TotalSizeClearedMB:F2} MB");
}
else
{
    Console.WriteLine($"Failed to clean {result.Failed.Count} locations");
}

// Clean only browser downloads
var browserResult = HtmlBrowserCacheCleaner.CleanAllCache(
    includeBrowsers: true,
    includeTemp: false);

// Get locations without cleaning (for inspection)
var tempOnly = HtmlBrowserCacheCleaner.GetCacheLocations(
    includeBrowsers: false,
    includeTemp: true);
foreach (var location in tempOnly)
{
    Console.WriteLine($"{location.Description}: {location.SizeMB:F2} MB at {location.Path}");
}
```

### Integration with Test Frameworks

#### xUnit Example
```csharp
[Fact]
public async Task WebsiteShouldHaveNoErrors()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    Assert.True(result.Passed, result.Summary);
    Assert.Empty(result.ConsoleErrors);
    Assert.Empty(result.FailedRequests);
}

[Fact]
public async Task CssShouldLoadQuickly()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    foreach (var css in result.CssResources)
    {
        Assert.True(css.Duration < TimeSpan.FromSeconds(2),
            $"CSS {css.Url} took {css.Duration?.TotalSeconds}s");
    }
}
```

#### Pester Example
```powershell
Describe "Website Health Check" {
    It "Should have no console errors" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.ErrorCount | Should -Be 0
    }

    It "Should load all resources successfully" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.FailedRequestCount | Should -Be 0
    }

    It "Should load within 5 seconds" {
        $metrics = Test-HtmlBrowser -Url "https://mysite.com" -PerformanceOnly
        $metrics.TotalLoadTime.TotalSeconds | Should -BeLessThan 5
    }
}
```

## 🔧 Troubleshooting

### Browser Extraction Mode

- **Playwright browser missing**: run `Test-HtmlBrowser -Install` or retry the command with `-Clean` when the browser cache is corrupt.
- **Corporate proxy or locked-down network**: pass `-Proxy` and `-ProxyCredential` on URL-based commands, or use `Set-HtmlHttpClientOption` for reusable module defaults.
- **Timeouts on app shells**: prefer `-RenderProfile AppShell -WaitForSelector 'main'` or `Wait-HtmlBrowserReady -Stable` instead of relying on network idle for applications that keep long-polling or WebSocket connections open.
- **Lazy content does not appear**: use `LazyLoadedContent`, `-AutoScroll`, `Invoke-HtmlBrowserScroll`, or a specific `-WaitForSelector` that represents the content you actually need.
- **Login reuse fails**: create storage state with the same browser engine and profile assumptions, then pass `-StorageStatePath` to the later render. Keep state files out of source control.
- **Response bodies contain secrets**: use `-IncludeResponseBody -RedactResponseBody`, keep `-ResponseBodyResourceType` narrow, and lower `-ResponseBodyMaxBytes` for large APIs.
- **Static HTML is already enough**: do not start a browser. Use `Test-HtmlExtractionPlan`, `Find-HtmlDataSource`, `Invoke-HtmlDataExtraction`, `ConvertFrom-Html*`, `Find-HtmlApiEndpoint`, crawl profiles, or `Compare-HtmlStaticRendered` to prove whether rendering adds value.
- **Endpoint extraction is skipped**: `Invoke-HtmlDataExtraction` does not fetch endpoint sources unless `-AllowHttpFetch` is supplied, and high-risk endpoints remain blocked. Review `RiskLevel`, `Warnings`, `IsExternal`, and `RequiresAuthenticationHint` before opting into endpoint reads.
- **Diagnostics look noisy**: `Get-HtmlBrowserDiagnostics` reports evidence such as `navigator.webdriver`, failed requests, console errors, and storage keys. Treat these as reliability hints, not a score to game.

### Jint and JavaScript Parser Notes

Current builds use Jint 4.x and Acornima for JavaScript parsing. Older examples
that reference Esprima or Jint 3.x types do not match the current API; use the
Acornima types exposed by `ConvertFrom-JavaScriptAst` and
`Select-JavaScriptAstNode`.

### Browser Testing Issues

If browser tests fail:

1. **First run downloads browsers automatically** - This can take a few minutes (~400MB)
   - You'll see: "Downloading Playwright driver... X% (Y MB/s)"
   - This only happens once per system

2. **Network timeout issues** - Some sites may be slow or blocked
   - Try increasing timeout: `Test-HtmlBrowser -Url $url -Timeout 60000`
   - Test with a simple URL first: `Test-HtmlBrowser -Url "http://httpbin.org/html"`

3. **Behind a proxy** - Set proxy environment variables:
   ```powershell
   $env:HTTPS_PROXY = "http://proxy:8080"
   $env:HTTP_PROXY = "http://proxy:8080"
   ```
   Or use proxy parameters:
   ```powershell
   Test-HtmlBrowser -Url $url -Proxy "http://proxy:8080" -ProxyCredential (Get-Credential)
   ```

4. **Clean and retry** if you suspect corrupted downloads:
   ```powershell
   Clear-HtmlBrowserCache -Force
   # Then run your test again - it will re-download browsers
   ```

5. **Manual browser installation** (C#):
   ```csharp
   // Ensure browser is installed before testing
   await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
   ```
