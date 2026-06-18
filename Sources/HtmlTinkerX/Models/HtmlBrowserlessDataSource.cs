using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Candidate data source that can be inspected or extracted without starting a browser.
/// </summary>
public sealed class HtmlBrowserlessDataSource {
    /// <summary>Source-order index within the browserless candidate list.</summary>
    public int Index { get; set; }

    /// <summary>Candidate kind, such as AppState, JsonLd, ScriptData, Microdata, OpenGraph, or ApiEndpoint.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best human-readable name for the source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional subtype or framework name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Page URL used as the discovery context, when known.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Original URL, endpoint path, or source-local identifier.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolved absolute URL when the source represents an HTTP endpoint.</summary>
    public string ResolvedUrl { get; set; } = string.Empty;

    /// <summary>Resolved URL with sensitive user-info, query, or fragment values redacted for display and logging.</summary>
    public string RedactedUrl { get; set; } = string.Empty;

    /// <summary>HTTP method when the source represents an endpoint.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Endpoint risk classification when the source represents an endpoint.</summary>
    public HtmlApiEndpointRiskLevel RiskLevel { get; set; } = HtmlApiEndpointRiskLevel.Low;

    /// <summary>Whether HtmlTinkerX can extract this source without additional browser work.</summary>
    public bool CanExtractDirectly { get; set; }

    /// <summary>Whether extraction requires an explicit HTTP fetch opt-in.</summary>
    public bool RequiresHttpFetch { get; set; }

    /// <summary>Whether the source points outside the page origin.</summary>
    public bool IsExternal { get; set; }

    /// <summary>Whether the source has authentication or token-related hints.</summary>
    public bool RequiresAuthenticationHint { get; set; }

    /// <summary>Observed request headers useful for understanding browser replay. Sensitive values are redacted.</summary>
    public IReadOnlyDictionary<string, string> ObservedRequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Non-sensitive observed request headers that can be copied into a replay request.</summary>
    public IReadOnlyDictionary<string, string> ReplayRequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Observed request header names whose values were redacted and should not be copied into scripts.</summary>
    public IReadOnlyList<string> SensitiveRequestHeaderNames { get; set; } = Array.Empty<string>();

    /// <summary>CSS-like selector or source hint for the source element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Short source label, such as Script, Meta, InlineScript, or LinkedScript.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Raw JSON, text, or endpoint metadata available at discovery time.</summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>Suggested PowerShell command for extracting this source after assigning it to <c>$source</c>.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Suggested PowerShell command for saving this source as a reusable extraction recipe after assigning it to <c>$source</c>.</summary>
    public string SuggestedRecipeCommand { get; set; } = string.Empty;

    /// <summary>Suggested PowerShell command for replaying the observed endpoint with safe session and header handling.</summary>
    public string SuggestedReplayCommand { get; set; } = string.Empty;

    /// <summary>Evidence explaining why this candidate is useful.</summary>
    public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();

    /// <summary>Warnings or operator review notes for this source.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
