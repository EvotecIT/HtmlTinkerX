namespace HtmlTinkerX;

/// <summary>
/// Structured callout, alert, note, or advisory block extracted from selected content.
/// </summary>
public sealed class HtmlCrawlStructuredCallout {
    /// <summary>Normalized callout kind such as note, warning, tip, or danger.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Short title when a heading-like label is present.</summary>
    public string? Title { get; set; }

    /// <summary>Normalized plain-text content of the callout.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Markdown representation of the callout content.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
