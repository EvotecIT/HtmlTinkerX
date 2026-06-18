namespace HtmlTinkerX;

/// <summary>
/// One preflight validation issue found in a browser automation recipe.
/// </summary>
public sealed class HtmlBrowserRecipeValidationIssue {
    /// <summary>Issue severity.</summary>
    public HtmlBrowserRecipeValidationSeverity Severity { get; set; }

    /// <summary>Zero-based step index when the issue belongs to a specific step.</summary>
    public int? StepIndex { get; set; }

    /// <summary>Step name when available.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Step action when available.</summary>
    public HtmlBrowserRecipeAction? Action { get; set; }

    /// <summary>Recipe or step property related to the issue.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>Human-readable issue message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Copy-ready or concise remediation guidance.</summary>
    public string SuggestedFix { get; set; } = string.Empty;

    /// <summary>Suggested command that helps inspect, repair, or rerun the recipe after this issue.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Short hint that points to the relevant workflow or command family.</summary>
    public string DocumentationHint { get; set; } = string.Empty;
}
