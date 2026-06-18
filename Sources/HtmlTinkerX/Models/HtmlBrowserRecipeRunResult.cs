using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of executing a browser automation recipe.
/// </summary>
public sealed class HtmlBrowserRecipeRunResult {
    /// <summary>Recipe name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>UTC start timestamp.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>UTC completion timestamp.</summary>
    public DateTimeOffset CompletedAtUtc { get; set; }

    /// <summary>Initial URL used by the recipe or supplied session.</summary>
    public string StartUrl { get; set; } = string.Empty;

    /// <summary>Final browser URL after all steps.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Final browser title after all steps.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether every non-continued step succeeded.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Whether the runner created and disposed the browser session.</summary>
    public bool CreatedSession { get; set; }

    /// <summary>Whether replay stopped before executing browser steps, usually because preflight validation failed.</summary>
    public bool SkippedBeforeExecution { get; set; }

    /// <summary>Validation result used before replay, when preflight validation was requested.</summary>
    public HtmlBrowserRecipeValidationResult? Validation { get; set; }

    /// <summary>Whether strict preflight mode treated warnings as blocking issues.</summary>
    public bool StrictPreflight { get; set; }

    /// <summary>Whether preflight validation blocked browser execution.</summary>
    public bool PreflightFailed => SkippedBeforeExecution && Validation != null;

    /// <summary>Index of the first failed step, or null when all steps succeeded.</summary>
    public int? FailedStepIndex { get; set; }

    /// <summary>Name of the first failed step, when available.</summary>
    public string FailedStepName { get; set; } = string.Empty;

    /// <summary>Short summary of the first failure and where it happened.</summary>
    public string FailureSummary { get; set; } = string.Empty;

    /// <summary>Copy-ready PowerShell command suggested for the first failed step.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Step results.</summary>
    public List<HtmlBrowserRecipeStepResult> Steps { get; set; } = new();
}
