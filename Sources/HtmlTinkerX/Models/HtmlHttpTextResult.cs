using System;

namespace HtmlTinkerX;

/// <summary>
/// Text downloaded over HTTP together with the final response URL after redirects.
/// </summary>
public sealed class HtmlHttpTextResult {
    /// <summary>Decoded response body.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Final response URL after redirects, when available.</summary>
    public Uri? FinalUri { get; set; }
}
