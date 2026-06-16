namespace HtmlTinkerX;

/// <summary>
/// Portable browserless extraction recipe produced from a discovered data source.
/// </summary>
public sealed class HtmlBrowserlessExtractionRecipe {
    /// <summary>Recipe schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Original page URL used during discovery, when known.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Source kind, such as AppState, JsonLd, ScriptData, or ApiEndpoint.</summary>
    public string SourceKind { get; set; } = string.Empty;

    /// <summary>Source name.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Optional source type or framework.</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Original source URL or endpoint path.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolved endpoint URL.</summary>
    public string ResolvedUrl { get; set; } = string.Empty;

    /// <summary>HTTP method when the source is an endpoint.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Endpoint risk classification captured during discovery.</summary>
    public HtmlApiEndpointRiskLevel RiskLevel { get; set; } = HtmlApiEndpointRiskLevel.Low;

    /// <summary>Whether the endpoint pointed outside the page origin during discovery.</summary>
    public bool IsExternal { get; set; }

    /// <summary>Whether the endpoint or page context contained authentication hints during discovery.</summary>
    public bool RequiresAuthenticationHint { get; set; }

    /// <summary>Selector or source hint.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Raw payload for static sources when explicitly included.</summary>
    public string RawContent { get; set; } = string.Empty;
}
