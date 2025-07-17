using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a single network request and response.
/// </summary>
public sealed class HtmlNetworkEntry {
    /// <summary>Request URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Request headers.</summary>
    public IDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

    /// <summary>Response status code.</summary>
    public int? Status { get; set; }

    /// <summary>Response headers.</summary>
    public IDictionary<string, string>? ResponseHeaders { get; set; }
}
