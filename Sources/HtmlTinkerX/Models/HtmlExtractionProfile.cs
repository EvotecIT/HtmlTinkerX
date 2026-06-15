using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Describes a reusable extraction workflow profile for a common page family.
/// </summary>
public sealed class HtmlExtractionProfile {
    /// <summary>Unique profile name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short user-facing profile label.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>What kind of page or workflow the profile is meant for.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Recommended extraction mode for this profile.</summary>
    public HtmlExtractionPlanMode RecommendedMode { get; set; } = HtmlExtractionPlanMode.Static;

    /// <summary>Built-in crawl profile to use when this workflow should crawl.</summary>
    public string? CrawlProfileName { get; set; }

    /// <summary>Browser rendering profile to use when this workflow should render.</summary>
    public HtmlRenderProfile? RenderProfile { get; set; }

    /// <summary>Whether this profile is suitable for dataset or LLM-ready output.</summary>
    public bool DatasetReady { get; set; }

    /// <summary>PowerShell command that demonstrates the profile workflow.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Reason codes or page signals that usually select this profile.</summary>
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
}
