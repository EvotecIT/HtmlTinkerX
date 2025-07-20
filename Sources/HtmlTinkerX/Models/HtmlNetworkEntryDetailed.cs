using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents detailed network request and response information with enhanced metrics.
/// </summary>
public sealed class HtmlNetworkEntryDetailed {
    /// <summary>Request URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP method.</summary>
    public HtmlHttpMethod Method { get; set; }

    /// <summary>Request headers.</summary>
    public IDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

    /// <summary>Response status code.</summary>
    public System.Net.HttpStatusCode? Status { get; set; }

    /// <summary>Response headers.</summary>
    public IDictionary<string, string>? ResponseHeaders { get; set; }

    /// <summary>Time when the request was issued.</summary>
    public System.DateTimeOffset Started { get; set; }

    /// <summary>Time when the first response was received.</summary>
    public System.DateTimeOffset? ResponseReceived { get; set; }

    /// <summary>Time when the request finished.</summary>
    public System.DateTimeOffset? Finished { get; set; }

    /// <summary>Duration of the request.</summary>
    public System.TimeSpan? Duration => Finished.HasValue ? Finished.Value - Started : null;
    
    /// <summary>Resource type classification.</summary>
    public HtmlNetworkResourceType ResourceType { get; set; }

    /// <summary>Size of the response body in bytes.</summary>
    public long? ResponseBodySize { get; set; }

    /// <summary>Size of request headers in bytes.</summary>
    public long? RequestHeadersSize { get; set; }

    /// <summary>Size of response headers in bytes.</summary>
    public long? ResponseHeadersSize { get; set; }

    /// <summary>Total size transferred over the network.</summary>
    public long? TransferSize => ResponseBodySize + ResponseHeadersSize;

    /// <summary>Indicates if the resource was served from cache.</summary>
    public bool ServedFromCache { get; set; }

    /// <summary>Error information if the request failed.</summary>
    public HtmlNetworkErrorType? ErrorType { get; set; }

    /// <summary>Error message if the request failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The initiator of the request (e.g., parser, script, other).</summary>
    public string Initiator { get; set; } = string.Empty;

    /// <summary>Priority of the request.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>Post data if this was a POST request.</summary>
    public string? PostData { get; set; }

    /// <summary>Response content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Response content encoding.</summary>
    public string? ContentEncoding { get; set; }

    /// <summary>Server timing information if available.</summary>
    public IDictionary<string, double>? ServerTiming { get; set; }

    /// <summary>Indicates if this request was blocked.</summary>
    public bool IsBlocked => ErrorType.HasValue;

    /// <summary>Indicates if this is a CSS resource.</summary>
    public bool IsCss => ResourceType == HtmlNetworkResourceType.Stylesheet;

    /// <summary>Indicates if this is a JavaScript resource.</summary>
    public bool IsJavaScript => ResourceType == HtmlNetworkResourceType.Script;

    /// <summary>Indicates if this is an image resource.</summary>
    public bool IsImage => ResourceType == HtmlNetworkResourceType.Image;

    /// <summary>Indicates if this is a font resource.</summary>
    public bool IsFont => ResourceType == HtmlNetworkResourceType.Font;

    /// <summary>Indicates if this is an AJAX/Fetch request.</summary>
    public bool IsAjax => ResourceType == HtmlNetworkResourceType.XHR || ResourceType == HtmlNetworkResourceType.Fetch;
}