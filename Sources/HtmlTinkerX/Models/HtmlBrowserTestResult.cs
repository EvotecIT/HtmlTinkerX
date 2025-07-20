using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Represents the result of browser testing including network and console analysis.
/// </summary>
public sealed class HtmlBrowserTestResult {
    /// <summary>URL that was tested.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Timestamp when the test was performed.</summary>
    public DateTimeOffset TestTimestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Total page load time.</summary>
    public TimeSpan? PageLoadTime { get; set; }

    /// <summary>All network entries captured during the test.</summary>
    public IList<HtmlNetworkEntryDetailed> NetworkEntries { get; set; } = new List<HtmlNetworkEntryDetailed>();

    /// <summary>All console entries captured during the test.</summary>
    public IList<HtmlConsoleEntryDetailed> ConsoleEntries { get; set; } = new List<HtmlConsoleEntryDetailed>();

    /// <summary>Gets console errors only.</summary>
    public IEnumerable<HtmlConsoleEntryDetailed> ConsoleErrors => ConsoleEntries.Where(e => e.IsError);

    /// <summary>Gets console warnings only.</summary>
    public IEnumerable<HtmlConsoleEntryDetailed> ConsoleWarnings => ConsoleEntries.Where(e => e.IsWarning);

    /// <summary>Gets failed network requests.</summary>
    public IEnumerable<HtmlNetworkEntryDetailed> FailedRequests => NetworkEntries.Where(e => e.IsBlocked);

    /// <summary>Gets CSS resources.</summary>
    public IEnumerable<HtmlNetworkEntryDetailed> CssResources => NetworkEntries.Where(e => e.IsCss);

    /// <summary>Gets JavaScript resources.</summary>
    public IEnumerable<HtmlNetworkEntryDetailed> JavaScriptResources => NetworkEntries.Where(e => e.IsJavaScript);

    /// <summary>Gets image resources.</summary>
    public IEnumerable<HtmlNetworkEntryDetailed> ImageResources => NetworkEntries.Where(e => e.IsImage);

    /// <summary>Total number of requests.</summary>
    public int TotalRequests => NetworkEntries.Count;

    /// <summary>Number of failed requests.</summary>
    public int FailedRequestCount => FailedRequests.Count();

    /// <summary>Number of console errors.</summary>
    public int ErrorCount => ConsoleErrors.Count();

    /// <summary>Number of console warnings.</summary>
    public int WarningCount => ConsoleWarnings.Count();

    /// <summary>Total bytes transferred.</summary>
    public long TotalBytesTransferred => NetworkEntries.Sum(e => e.TransferSize ?? 0);

    /// <summary>Indicates if the test passed (no errors or failed requests).</summary>
    public bool Passed => ErrorCount == 0 && FailedRequestCount == 0;

    /// <summary>Gets a summary of issues found.</summary>
    public string Summary {
        get {
            if (Passed)
                return "All tests passed. No errors or failed requests found.";

            var issues = new List<string>();
            if (ErrorCount > 0)
                issues.Add($"{ErrorCount} console error(s)");
            if (WarningCount > 0)
                issues.Add($"{WarningCount} console warning(s)");
            if (FailedRequestCount > 0)
                issues.Add($"{FailedRequestCount} failed request(s)");

            return $"Issues found: {string.Join(", ", issues)}";
        }
    }

    /// <summary>Gets performance metrics.</summary>
    public HtmlPerformanceMetrics GetPerformanceMetrics() {
        return new HtmlPerformanceMetrics {
            TotalLoadTime = PageLoadTime,
            TotalRequests = TotalRequests,
            TotalBytesTransferred = TotalBytesTransferred,
            AverageRequestDuration = NetworkEntries.Any() 
                ? TimeSpan.FromMilliseconds(NetworkEntries.Average(e => (e.Duration ?? TimeSpan.Zero).TotalMilliseconds))
                : TimeSpan.Zero,
            LongestRequest = NetworkEntries
                .Where(e => e.Duration.HasValue)
                .OrderByDescending(e => e.Duration!.Value)
                .FirstOrDefault(),
            ResourceBreakdown = NetworkEntries
                .GroupBy(e => e.ResourceType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}