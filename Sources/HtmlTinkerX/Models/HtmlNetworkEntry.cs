using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a single network request and response.
/// </summary>
public sealed class HtmlNetworkEntry {
    /// <summary>Request URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP method.</summary>
    public HtmlHttpMethod Method { get; set; }

    /// <summary>Request headers.</summary>
    public IDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

    /// <summary>Browser-reported resource type when available.</summary>
    public HtmlNetworkResourceType ResourceType { get; set; } = HtmlNetworkResourceType.Other;

    /// <summary>Response status code.</summary>
    public System.Net.HttpStatusCode? Status { get; set; }

    /// <summary>Response headers.</summary>
    public IDictionary<string, string>? ResponseHeaders { get; set; }

    /// <summary>Optional captured response body. This is populated only when explicitly requested.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Whether <see cref="ResponseBody"/> was truncated to the configured capture limit.</summary>
    public bool ResponseBodyTruncated { get; set; }

    /// <summary>Error reported while capturing the response body, when capture was requested but unavailable.</summary>
    public string? ResponseBodyError { get; set; }

    /// <summary>Time when the request was issued.</summary>
    public System.DateTimeOffset Started { get; set; }

    /// <summary>Time when the first response was received.</summary>
    public System.DateTimeOffset? ResponseReceived { get; set; }

    /// <summary>Time when the request finished.</summary>
    public System.DateTimeOffset? Finished { get; set; }

    /// <summary>Duration of the request.</summary>
    public System.TimeSpan? Duration => Finished.HasValue ? Finished.Value - Started : null;
}
