namespace HtmlTinkerX;

/// <summary>
/// Represents text extracted from the most readable content region of an HTML document.
/// </summary>
public sealed class HtmlReadableTextResult {
    /// <summary>Plain text extracted from the selected readable region.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Best title found near the selected region or document title.</summary>
    public string? Title { get; set; }

    /// <summary>CSS-like selector hint for the selected element.</summary>
    public string? SelectorHint { get; set; }

    /// <summary>Reader score assigned to the selected element.</summary>
    public double Score { get; set; }

    /// <summary>Number of candidate elements considered.</summary>
    public int CandidateCount { get; set; }
}
