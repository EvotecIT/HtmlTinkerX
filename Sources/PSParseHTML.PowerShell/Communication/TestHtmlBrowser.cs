using System.Management.Automation;
using System.Threading.Tasks;
using System.Linq;
using HtmlTinkerX;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Tests a URL or local HTML file for network errors, console errors, and performance issues.
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "HtmlBrowser")]
[OutputType(typeof(HtmlBrowserTestResult))]
public sealed class TestHtmlBrowserCommand : AsyncPSCmdlet {
    /// <summary>
    /// URL to test.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = "Url")]
    [ValidateNotNullOrEmpty]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Path to local HTML file to test.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Browser engine to use.
    /// </summary>
    [Parameter()]
    public HtmlBrowserEngine Engine { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    [Parameter()]
    [ValidateRange(1000, 300000)]
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// Enable headless mode.
    /// </summary>
    [Parameter()]
    public SwitchParameter Headless { get; set; } = true;

    /// <summary>
    /// Proxy URL to use.
    /// </summary>
    [Parameter()]
    public string? Proxy { get; set; }

    /// <summary>
    /// Proxy credentials.
    /// </summary>
    [Parameter()]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>
    /// Return only performance metrics.
    /// </summary>
    [Parameter()]
    public SwitchParameter PerformanceOnly { get; set; }

    /// <summary>
    /// Return only console errors.
    /// </summary>
    [Parameter()]
    public SwitchParameter ErrorsOnly { get; set; }

    /// <summary>
    /// Test for specific CSS resource.
    /// </summary>
    [Parameter()]
    public string? CssResource { get; set; }

    /// <summary>
    /// Processes the record asynchronously.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        string? proxyUsername = null;
        string? proxyPassword = null;

        if (!string.IsNullOrEmpty(Proxy) && ProxyCredential != null) {
            proxyUsername = ProxyCredential.UserName;
            proxyPassword = ProxyCredential.GetNetworkCredential()?.Password;
        }

        // Determine if testing URL or file
        bool isFile = ParameterSetName == "File";
        string targetUrl = isFile ? Path : Url;

        if (!string.IsNullOrEmpty(CssResource)) {
            WriteVerbose($"Testing for CSS resource: {CssResource}");

            HtmlNetworkEntryDetailed? cssEntry;
            if (isFile) {
                // For file testing, first run full test then extract CSS
                var fileResult = await HtmlBrowserTester.TestFileAsync(Path, Engine, Headless, Timeout);
                cssEntry = fileResult.CssResources.FirstOrDefault(r => r.Url.Contains(CssResource));
            } else {
                cssEntry = await HtmlBrowserTester.TestCssResourceAsync(Url, CssResource!, Engine);
            }

            if (cssEntry != null) {
                WriteObject(cssEntry);
            } else {
                WriteWarning($"CSS resource '{CssResource}' not found in network log");
            }
            return;
        }

        if (ErrorsOnly) {
            WriteVerbose("Testing for console errors only");

            IList<HtmlConsoleEntryDetailed> errors;
            if (isFile) {
                var fileResult = await HtmlBrowserTester.TestFileAsync(Path, Engine, Headless, Timeout);
                errors = fileResult.ConsoleErrors.ToList();
            } else {
                errors = await HtmlBrowserTester.TestConsoleErrorsAsync(Url, Engine);
            }

            foreach (var error in errors) {
                WriteObject(error);
            }

            if (errors.Count == 0) {
                WriteVerbose("No console errors found");
            }
            return;
        }

        if (PerformanceOnly) {
            WriteVerbose("Testing performance metrics only");

            HtmlPerformanceMetrics metrics;
            if (isFile) {
                var fileResult = await HtmlBrowserTester.TestFileAsync(Path, Engine, Headless, Timeout);
                metrics = fileResult.GetPerformanceMetrics();
            } else {
                metrics = await HtmlBrowserTester.TestPerformanceAsync(Url, Engine);
            }

            WriteObject(metrics);
            return;
        }

        // Full test
        WriteVerbose($"Running full browser test on: {targetUrl}");

        HtmlBrowserTestResult result;
        if (isFile) {
            result = await HtmlBrowserTester.TestFileAsync(Path, Engine, Headless, Timeout);
        } else {
            result = await HtmlBrowserTester.TestUrlAsync(
                Url,
                Engine,
                Headless,
                Timeout,
                Proxy,
                proxyUsername,
                proxyPassword);
        }

        WriteVerbose($"Test completed. {result.Summary}");
        WriteObject(result);
    }
}

/// <summary>
/// Cleans up Playwright browser downloads and cache.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "HtmlBrowserCache")]
public sealed class ClearHtmlBrowserCacheCommand : PSCmdlet {
    /// <summary>
    /// Force cleanup without confirmation.
    /// </summary>
    [Parameter()]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Skip browser downloads cleanup.
    /// </summary>
    [Parameter()]
    public SwitchParameter SkipBrowsers { get; set; }

    /// <summary>
    /// Skip temporary files cleanup.
    /// </summary>
    [Parameter()]
    public SwitchParameter SkipTemp { get; set; }

    /// <summary>
    /// Processes the record.
    /// </summary>
    protected override void ProcessRecord() {
        // Get cache locations
        var locations = HtmlBrowserCacheCleaner.GetCacheLocations(
            includeBrowsers: !SkipBrowsers,
            includeTemp: !SkipTemp
        );
        
        if (locations.Count == 0) {
            WriteVerbose("No Playwright cache or temp files found");
            return;
        }
        
        var totalSizeMB = locations.Sum(l => l.SizeMB);
        
        // Display what will be cleaned
        WriteObject($"Found {locations.Count} location(s) with {totalSizeMB:F2} MB to clean:");
        foreach (var location in locations) {
            WriteObject($"  - {location.Description}: {location.SizeMB:F2} MB at {location.Path}");
        }
        
        if (!Force && !ShouldProcess(
            $"Playwright cache and temp files ({totalSizeMB:F2} MB total)",
            "Clear")) {
            return;
        }
        
        // Perform the cleaning
        var result = HtmlBrowserCacheCleaner.CleanCache(locations);
        
        // Report results
        foreach (var location in result.SuccessfullyCleared) {
            WriteVerbose($"Cleared {location.Description} at: {location.Path}");
        }
        
        foreach (var (location, error) in result.Failed) {
            WriteWarning($"Failed to clear {location.Description}: {error}");
        }
        
        if (result.SuccessfullyCleared.Count > 0) {
            WriteObject($"Successfully cleared {result.SuccessfullyCleared.Count} location(s), {result.TotalSizeClearedMB:F2} MB total");
        }
        
        if (!result.Success) {
            WriteError(new ErrorRecord(
                new System.InvalidOperationException($"Failed to clear {result.Failed.Count} location(s)"),
                "PartialClearFailure",
                ErrorCategory.WriteError,
                result.Failed.Select(f => $"{f.Location.Description}: {f.Error}").ToArray()
            ));
        }
    }
}