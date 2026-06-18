using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Replayable browser automation recipe for HtmlTinkerX.
/// </summary>
public sealed class HtmlBrowserRecipe {
    /// <summary>Recipe schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Optional recipe name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL used to open a new browser session when no session is supplied by the caller.</summary>
    public string? StartUrl { get; set; }

    /// <summary>Browser engine used when the recipe opens its own session.</summary>
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Run the browser without a visible window when the recipe opens its own session.</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Initial load state used when the recipe opens its own session.</summary>
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Default timeout in milliseconds for steps that do not specify their own timeout.</summary>
    public int Timeout { get; set; } = 10000;

    /// <summary>Whether failure evidence should be exported when a step fails.</summary>
    public bool OnFailureEvidence { get; set; }

    /// <summary>Root folder for failure evidence.</summary>
    public string? FailureEvidenceFolder { get; set; }

    /// <summary>Automation steps to execute.</summary>
    public List<HtmlBrowserRecipeStep> Steps { get; set; } = new();
}
