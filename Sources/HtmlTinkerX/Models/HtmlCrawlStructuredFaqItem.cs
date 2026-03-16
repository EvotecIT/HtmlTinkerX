namespace HtmlTinkerX;

/// <summary>
/// Structured FAQ-style question and answer extracted from the selected content.
/// </summary>
public sealed class HtmlCrawlStructuredFaqItem {
    /// <summary>Question or prompt text.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Answer text with whitespace normalized for ingestion.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Markdown representation of the answer when available.</summary>
    public string AnswerMarkdown { get; set; } = string.Empty;

    /// <summary>Extraction source category, such as microdata or details.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
