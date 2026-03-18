namespace HtmlTinkerX;

/// <summary>
/// Represents a queued crawl candidate that has not been fetched yet.
/// </summary>
public sealed class HtmlCrawlPendingItem {
    /// <summary>Candidate URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Parent page that discovered the candidate.</summary>
    public string? ParentUrl { get; set; }

    /// <summary>Depth at which the candidate will be crawled.</summary>
    public int Depth { get; set; }

    /// <summary>Offline readiness grade for pending candidates, which have not been assessed yet.</summary>
    public string OfflineReadinessGrade { get; set; } = "not-assessed";

    /// <summary>Highest offline-runtime risk severity for pending candidates, which have not been assessed yet.</summary>
    public string HighestOfflineRiskSeverity { get; set; } = "none";

    /// <summary>Count of recorded offline-runtime dependency diagnostics for pending candidates, which have not been assessed yet.</summary>
    public int OfflineDependencyDiagnosticCount { get; set; }

    /// <summary>Compact summary of distinct offline-runtime dependency kinds for pending candidates, which have not been assessed yet.</summary>
    public string OfflineDependencyKindsSummary { get; set; } = string.Empty;
}
