using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of following browserless hidden-form relay pages.
/// </summary>
public sealed class HtmlFormRelayResult {
    /// <summary>Final response content after relay processing stopped.</summary>
    public string FinalContent { get; set; } = string.Empty;

    /// <summary>Final response URL after relay processing stopped.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Reason relay processing stopped.</summary>
    public HtmlFormRelayStopReason StopReason { get; set; } = HtmlFormRelayStopReason.NoRelayForm;

    /// <summary>Whether at least one relay form was submitted.</summary>
    public bool SubmittedRelay { get; set; }

    /// <summary>Relay steps that were submitted or blocked. Field values are intentionally omitted.</summary>
    public IReadOnlyList<HtmlFormRelayStep> Steps { get; set; } = new List<HtmlFormRelayStep>();
}
