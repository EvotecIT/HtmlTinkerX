namespace HtmlTinkerX;

/// <summary>
/// Confidence level assigned to an extraction plan recommendation.
/// </summary>
public enum HtmlExtractionPlanConfidence {
    /// <summary>The recommendation is based on weak or limited signals.</summary>
    Low,
    /// <summary>The recommendation is based on several useful signals.</summary>
    Medium,
    /// <summary>The recommendation is based on strong, specific page signals.</summary>
    High
}
