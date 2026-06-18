using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Redacted report entry for one selector hardening step.
/// </summary>
public sealed class HtmlBrowserRecipeHardeningReportStep {
    /// <summary>Zero-based recipe step index.</summary>
    public int StepIndex { get; set; }

    /// <summary>Recipe step name when available.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Recipe action.</summary>
    public HtmlBrowserRecipeAction Action { get; set; }

    /// <summary>Redacted primary selector inspected for this step.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Whether the step was eligible for selector hardening.</summary>
    public bool Eligible { get; set; }

    /// <summary>Whether the hardening pass changed this step.</summary>
    public bool Changed { get; set; }

    /// <summary>Redacted selector alternates added to this step.</summary>
    public List<string> AddedAlternates { get; set; } = new();

    /// <summary>Redacted selector alternates already present before hardening.</summary>
    public List<string> ExistingAlternates { get; set; } = new();

    /// <summary>Reason this step was changed, skipped, or left unchanged.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Redacted suggested follow-up command when the step still needs attention.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;
}
