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
}
