namespace HtmlTinkerX;

/// <summary>
/// Severity of a browser recipe preflight validation issue.
/// </summary>
public enum HtmlBrowserRecipeValidationSeverity {
    /// <summary>Informational guidance that does not block execution.</summary>
    Information,

    /// <summary>Risk or missing context that may make the recipe unreliable.</summary>
    Warning,

    /// <summary>Problem that is expected to make recipe execution fail.</summary>
    Error
}
