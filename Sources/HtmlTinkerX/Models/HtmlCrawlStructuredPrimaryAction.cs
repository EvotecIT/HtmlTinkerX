namespace HtmlTinkerX;

/// <summary>
/// Prominent action link or button extracted from selected content.
/// </summary>
public sealed class HtmlCrawlStructuredPrimaryAction {
    /// <summary>Visible label presented to the user.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Resolved URL when the action is link-based.</summary>
    public string? Url { get; set; }

    /// <summary>Action element type such as link, button, or submit.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Normalized action intent such as install, download, buy, or start.</summary>
    public string Intent { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
