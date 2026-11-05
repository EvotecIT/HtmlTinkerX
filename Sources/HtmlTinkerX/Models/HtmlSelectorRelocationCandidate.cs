using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Element that may be the new location of content a saved selector no longer finds. A candidate is a suggestion:
/// review it before updating the selector.
/// </summary>
public sealed class HtmlSelectorRelocationCandidate {
    /// <summary>Zero-based candidate index after ranking.</summary>
    public int Index { get; set; }

    /// <summary>Suggested CSS selector for the candidate element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>High, Medium or Low, using the same thresholds as collection confidence.</summary>
    public string Confidence { get; set; } = string.Empty;

    /// <summary>Similarity score; higher means more of the fingerprint matched.</summary>
    public int Score { get; set; }

    /// <summary>Number of elements the suggested selector matches in the current document.</summary>
    public int MatchCount { get; set; }

    /// <summary>Fingerprint signals that matched or changed, for review.</summary>
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();

    /// <summary>Normalized text from the candidate element, shortened to 240 characters.</summary>
    public string SampleText { get; set; } = string.Empty;
}
