namespace HtmlTinkerX;

/// <summary>
/// HTTP methods used for network requests.
/// </summary>
public enum HtmlHttpMethod {
    /// <summary>GET request.</summary>
    Get,
    /// <summary>HEAD request.</summary>
    Head,
    /// <summary>POST request.</summary>
    Post,
    /// <summary>PUT request.</summary>
    Put,
    /// <summary>DELETE request.</summary>
    Delete,
    /// <summary>CONNECT request.</summary>
    Connect,
    /// <summary>OPTIONS request.</summary>
    Options,
    /// <summary>TRACE request.</summary>
    Trace,
    /// <summary>PATCH request.</summary>
    Patch,
    /// <summary>Other or unknown method.</summary>
    Other
}
