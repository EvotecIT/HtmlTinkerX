namespace HtmlTinkerX;

/// <summary>
/// One item within a structured breadcrumb trail.
/// </summary>
public sealed class HtmlCrawlStructuredBreadcrumbItem {
    /// <summary>Visible breadcrumb label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Resolved breadcrumb URL when available.</summary>
    public string? Url { get; set; }

    /// <summary>Whether this item represents the current page.</summary>
    public bool IsCurrent { get; set; }
}
