using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of one browser recipe step.
/// </summary>
public sealed class HtmlBrowserRecipeStepResult {
    /// <summary>Zero-based step index.</summary>
    public int Index { get; set; }

    /// <summary>Step name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Action that was executed.</summary>
    public HtmlBrowserRecipeAction Action { get; set; }

    /// <summary>Primary target for the step.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Selector that ultimately worked for this step when selector fallback was used.</summary>
    public string SelectedSelector { get; set; } = string.Empty;

    /// <summary>Selectors attempted for this step in replay order.</summary>
    public IReadOnlyList<string> AttemptedSelectors { get; set; } = Array.Empty<string>();

    /// <summary>UTC start timestamp.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>UTC completion timestamp.</summary>
    public DateTimeOffset CompletedAtUtc { get; set; }

    /// <summary>Whether the step succeeded.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Error type when the step failed.</summary>
    public string? ErrorType { get; set; }

    /// <summary>Error message when the step failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Browser page URL observed after the step finished or failed.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Browser page title observed after the step finished or failed.</summary>
    public string PageTitle { get; set; } = string.Empty;

    /// <summary>Human-readable guidance for fixing or retrying a failed step.</summary>
    public string SuggestedFix { get; set; } = string.Empty;

    /// <summary>Copy-ready PowerShell command that helps verify or retry a failed step.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Optional step output.</summary>
    public string? Output { get; set; }

    /// <summary>Evidence result produced by Evidence or failure-evidence steps.</summary>
    public HtmlBrowserEvidenceResult? Evidence { get; set; }

    /// <summary>Locator candidates produced by Locator steps.</summary>
    public IReadOnlyList<HtmlBrowserLocatorCandidate>? LocatorCandidates { get; set; }
}
