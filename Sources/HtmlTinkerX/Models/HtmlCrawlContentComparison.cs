namespace HtmlTinkerX;

/// <summary>
/// Summarizes how one extraction mode would treat a crawled page without storing a second full copy of the content.
/// </summary>
public sealed class HtmlCrawlContentComparison {
    /// <summary>Extraction mode used for this comparison.</summary>
    public HtmlCrawlContentMode Mode { get; set; }

    /// <summary>Stable category describing how the extraction mode selected its content.</summary>
    public HtmlCrawlContentSelectionReasonCode ReasonCode { get; set; }

    /// <summary>Human-readable explanation of the extraction decision.</summary>
    public string? Reason { get; set; }

    /// <summary>Compact selector-like hint for the selected element when one was chosen.</summary>
    public string? ElementSelectorHint { get; set; }

    /// <summary>Word count of the extracted text after cleanup.</summary>
    public int WordCount { get; set; }

    /// <summary>Character count of the extracted text after cleanup.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Short summary snippet of the extracted text after cleanup.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Reader-mode score for the final selected content element when available.</summary>
    public double? Score { get; set; }

    /// <summary>Number of reader-mode candidate blocks that were evaluated for this comparison.</summary>
    public int ReaderCandidateCount { get; set; }

    /// <summary>Compact selector-like hint for the reader root element when reader-mode scoring was used.</summary>
    public string? ReaderRootElementSelectorHint { get; set; }
}
