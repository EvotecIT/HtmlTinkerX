namespace HtmlTinkerX;

/// <summary>
/// HTTP request performed by browserless extraction.
/// </summary>
public sealed class HtmlBrowserlessExtractionRequest {
    /// <summary>HTTP method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Request URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP status code when a response was received.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Response content type when available.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Whether the request completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when the request failed before a useful response was available.</summary>
    public string Error { get; set; } = string.Empty;
}
