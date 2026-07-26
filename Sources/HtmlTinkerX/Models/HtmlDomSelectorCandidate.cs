using System;

namespace HtmlTinkerX;

/// <summary>
/// Ranked repeated-structure candidate with suggested fields and an extraction command or secure command template.
/// </summary>
public sealed class HtmlDomSelectorCandidate {
    /// <summary>Zero-based candidate index after ranking.</summary>
    public int Index { get; set; }

    /// <summary>CSS selector matching the repeated item structure.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>HTML tag used by the repeated item.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Number of elements matched by <see cref="Selector"/>.</summary>
    public int MatchCount { get; set; }

    /// <summary>Candidate quality score; higher values are more likely to represent useful records.</summary>
    public int Score { get; set; }

    /// <summary>Short explanation of why the candidate was ranked.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Representative normalized item text.</summary>
    public string SampleText { get; set; } = string.Empty;

    /// <summary>Fields and links discovered relative to the repeated item.</summary>
    public HtmlDomSelectorFieldCandidate[] Fields { get; set; } = Array.Empty<HtmlDomSelectorFieldCandidate>();

    /// <summary>Select-HtmlData command using the discovered item and field selectors.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Whether <see cref="SuggestedCommand"/> can run without supplying prerequisite variables.</summary>
    public bool SuggestedCommandIsReplayable { get; set; }

    /// <summary>Prerequisites for secure templates, or an empty string for a directly replayable command.</summary>
    public string SuggestedCommandNote { get; set; } = string.Empty;
}
