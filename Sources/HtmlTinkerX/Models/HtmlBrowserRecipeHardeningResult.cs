using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Result of hardening a browser recipe with selector alternates from a live page.
/// </summary>
public sealed class HtmlBrowserRecipeHardeningResult {
    /// <summary>Recipe name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Recipe after hardening. The original object passed to the engine is also updated.</summary>
    public HtmlBrowserRecipe Recipe { get; set; } = new();

    /// <summary>Total number of recipe steps inspected.</summary>
    public int StepCount { get; set; }

    /// <summary>Number of selector-based steps eligible for hardening.</summary>
    public int EligibleStepCount => Steps.Count(step => step.Eligible);

    /// <summary>Number of steps changed by the hardening pass.</summary>
    public int ChangedStepCount => Steps.Count(step => step.Changed);

    /// <summary>Total number of selector alternates added.</summary>
    public int AddedAlternateCount => Steps.Sum(step => step.AddedAlternates.Count);

    /// <summary>Whether the hardening pass changed any step.</summary>
    public bool Changed => ChangedStepCount > 0;

    /// <summary>Path where a redacted hardening report was saved, when requested.</summary>
    public string ReportPath { get; set; } = string.Empty;

    /// <summary>Per-step hardening details.</summary>
    public List<HtmlBrowserRecipeHardeningStepResult> Steps { get; set; } = new();

    /// <summary>Short human-readable summary suitable for logs.</summary>
    public string Summary => Changed
        ? $"Hardened {ChangedStepCount} of {EligibleStepCount} eligible selector step(s), adding {AddedAlternateCount} selector alternate(s)."
        : $"No selector alternates were added across {EligibleStepCount} eligible selector step(s).";
}
