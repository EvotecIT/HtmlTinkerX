namespace HtmlTinkerX;

/// <summary>
/// Structured representation of a code block captured from the selected content.
/// </summary>
public sealed class HtmlCrawlStructuredCodeBlock {
    /// <summary>Detected programming or markup language when available.</summary>
    public string? Language { get; set; }

    /// <summary>Code text with line breaks preserved.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Number of lines in the code block.</summary>
    public int LineCount { get; set; }

    /// <summary>Number of characters in the code block.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
