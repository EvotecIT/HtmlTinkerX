using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents performance metrics for a web page load.
/// </summary>
public sealed class HtmlPerformanceMetrics {
    /// <summary>Total time to load the page.</summary>
    public TimeSpan? TotalLoadTime { get; set; }

    /// <summary>Total number of network requests.</summary>
    public int TotalRequests { get; set; }

    /// <summary>Total bytes transferred.</summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>Average request duration.</summary>
    public TimeSpan AverageRequestDuration { get; set; }

    /// <summary>The longest running request.</summary>
    public HtmlNetworkEntryDetailed? LongestRequest { get; set; }

    /// <summary>Breakdown of requests by resource type.</summary>
    public IDictionary<HtmlNetworkResourceType, int> ResourceBreakdown { get; set; } = new Dictionary<HtmlNetworkResourceType, int>();

    /// <summary>
    /// Gets a formatted report of the metrics.
    /// </summary>
    public string GetReport() {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Performance Metrics ===");
        report.AppendLine($"Total Load Time: {TotalLoadTime?.TotalMilliseconds ?? 0:F2}ms");
        report.AppendLine($"Total Requests: {TotalRequests}");
        report.AppendLine($"Total Bytes: {TotalBytesTransferred:N0} bytes ({TotalBytesTransferred / 1024.0:F2} KB)");
        report.AppendLine($"Average Request Duration: {AverageRequestDuration.TotalMilliseconds:F2}ms");
        
        if (LongestRequest != null) {
            report.AppendLine($"Longest Request: {LongestRequest.Duration?.TotalMilliseconds ?? 0:F2}ms - {LongestRequest.Url}");
        }

        if (ResourceBreakdown.Count > 0) {
            report.AppendLine("\nResource Breakdown:");
            foreach (var kvp in ResourceBreakdown) {
                report.AppendLine($"  {kvp.Key}: {kvp.Value} requests");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Returns a formatted string representing the metrics.
    /// </summary>
    public override string ToString() => GetReport();
}