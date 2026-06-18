using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of hardening selector alternates for one browser recipe step.
/// </summary>
public sealed class HtmlBrowserRecipeHardeningStepResult {
    /// <summary>Zero-based recipe step index.</summary>
    public int StepIndex { get; set; }

    /// <summary>Recipe step name when available.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Recipe action.</summary>
    public HtmlBrowserRecipeAction Action { get; set; }

    /// <summary>Primary selector inspected for this step.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Whether the step was eligible for selector hardening.</summary>
    public bool Eligible { get; set; }

    /// <summary>Whether the hardening pass changed this step.</summary>
    public bool Changed { get; set; }

    /// <summary>Selector alternates added to this step.</summary>
    public List<string> AddedAlternates { get; set; } = new();

    /// <summary>Selector alternates already present before hardening.</summary>
    public List<string> ExistingAlternates { get; set; } = new();

    /// <summary>Reason this step was changed, skipped, or left unchanged.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Suggested follow-up command when the step still needs attention.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;
}
