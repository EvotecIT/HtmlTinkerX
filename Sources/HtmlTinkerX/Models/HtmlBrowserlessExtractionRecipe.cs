namespace HtmlTinkerX;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Portable browserless extraction recipe produced from a discovered data source.
/// </summary>
public sealed class HtmlBrowserlessExtractionRecipe {
    /// <summary>Recipe schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Original page URL used during discovery, when known.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Source kind, such as AppState, JsonLd, ScriptData, ApiEndpoint, or Dom.</summary>
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

    /// <summary>Non-sensitive request headers captured during discovery that are safe to replay.</summary>
    public IDictionary<string, string> ReplayRequestHeaders { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Selector or source hint.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Raw payload for static sources when explicitly included.</summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>
    /// DOM field rules evaluated relative to Selector when SourceKind is Dom. Saved types use
    /// portable primitive names or the full name of a non-generic enum available to the importing process.
    /// Defaults support scalar values and enums; arbitrary CLR objects cannot be saved.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, HtmlDomFieldDefinition>? DomProperties { get; set; }

    /// <summary>Optional minimum number of items required by a DOM recipe.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinimumItemCount { get; set; }

    /// <summary>Optional maximum number of items accepted by a DOM recipe.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximumItemCount { get; set; }
}