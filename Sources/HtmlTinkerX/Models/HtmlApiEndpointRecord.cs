using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Classified endpoint discovered from forms, inline JavaScript, or linked JavaScript.
/// </summary>
public sealed class HtmlApiEndpointRecord {
    /// <summary>Endpoint family such as Form, Endpoint, or LinkedEndpoint.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best available operation or endpoint name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HTTP method when known.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Original endpoint URL or path.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolved absolute endpoint URL when a base URL is known.</summary>
    public string ResolvedUrl { get; set; } = string.Empty;

    /// <summary>Endpoint origin when the URL is absolute or resolvable.</summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>Whether the endpoint points outside the page origin.</summary>
    public bool IsExternal { get; set; }

    /// <summary>Whether the endpoint method can change server state.</summary>
    public bool IsStateChanging { get; set; }

    /// <summary>Whether the URL or page context contains auth-related hints.</summary>
    public bool RequiresAuthenticationHint { get; set; }

    /// <summary>Whether the query string contains sensitive parameter names. Values are not stored.</summary>
    public bool HasSensitiveQuery { get; set; }

    /// <summary>Endpoint risk classification.</summary>
    public HtmlApiEndpointRiskLevel RiskLevel { get; set; } = HtmlApiEndpointRiskLevel.Low;

    /// <summary>Reason codes explaining the risk classification.</summary>
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();

    /// <summary>CSS-like selector hint for the source element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Source label such as Form, InlineScript, or LinkedScript.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Additional source metadata from the interaction surface.</summary>
    public string Metadata { get; set; } = string.Empty;
}
