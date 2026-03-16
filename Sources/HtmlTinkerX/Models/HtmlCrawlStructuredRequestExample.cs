using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured request example associated with an inferred API endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredRequestExample {
    /// <summary>Short title or nearby heading for the request example.</summary>
    public string? Title { get; set; }

    /// <summary>Nearby description or prose documenting the request when available.</summary>
    public string? Description { get; set; }

    /// <summary>Detected content language or format.</summary>
    public string? Language { get; set; }

    /// <summary>Normalized request example kind such as http, curl, json, or command.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>HTTP method when one can be inferred from the example.</summary>
    public string? Method { get; set; }

    /// <summary>Normalized request path when one can be inferred from the example.</summary>
    public string? Path { get; set; }

    /// <summary>Headers documented alongside the request.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> Headers { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Detected content type when one can be inferred from headers or body.</summary>
    public string? ContentType { get; set; }

    /// <summary>Request body or example text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
