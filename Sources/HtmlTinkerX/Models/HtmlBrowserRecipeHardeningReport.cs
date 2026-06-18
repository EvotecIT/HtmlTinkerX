using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Redacted JSON-safe report for a browser recipe selector hardening pass.
/// </summary>
public sealed class HtmlBrowserRecipeHardeningReport {
    /// <summary>UTC timestamp when the report was generated.</summary>
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Recipe name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Total number of recipe steps inspected.</summary>
    public int StepCount { get; set; }

    /// <summary>Number of selector-based steps eligible for hardening.</summary>
    public int EligibleStepCount { get; set; }

    /// <summary>Number of steps changed by the hardening pass.</summary>
    public int ChangedStepCount { get; set; }

    /// <summary>Total number of selector alternates added.</summary>
    public int AddedAlternateCount { get; set; }

    /// <summary>Whether the hardening pass changed any step.</summary>
    public bool Changed { get; set; }

    /// <summary>Short human-readable summary suitable for logs.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Per-step redacted hardening details.</summary>
    public List<HtmlBrowserRecipeHardeningReportStep> Steps { get; set; } = new();
}
