using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured breadcrumb trail kept separate from the main document content.
/// </summary>
public sealed class HtmlCrawlStructuredBreadcrumbTrail {
    /// <summary>Ordered breadcrumb items.</summary>
    public IList<HtmlCrawlStructuredBreadcrumbItem> Items { get; set; } = new List<HtmlCrawlStructuredBreadcrumbItem>();

    /// <summary>Flattened breadcrumb labels for quick downstream access.</summary>
    public IList<string> Labels { get; set; } = new List<string>();

    /// <summary>Flattened breadcrumb URLs for quick downstream access.</summary>
    public IList<string> Urls { get; set; } = new List<string>();

    /// <summary>The current page label when the trail marks it explicitly.</summary>
    public string? CurrentLabel { get; set; }

    /// <summary>Compact selector-like hint for the breadcrumb container.</summary>
    public string? SelectorHint { get; set; }
}
