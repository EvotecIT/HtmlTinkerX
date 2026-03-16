using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured response example associated with an inferred API endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredResponseExample {
    /// <summary>Short title or nearby heading for the response example.</summary>
    public string? Title { get; set; }

    /// <summary>Nearby description or prose documenting the response when available.</summary>
    public string? Description { get; set; }

    /// <summary>Detected content language or format.</summary>
    public string? Language { get; set; }

    /// <summary>Normalized response example kind such as json or http.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Status code when one can be inferred from the heading or example.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Status text when one can be inferred from the heading or example.</summary>
    public string? StatusText { get; set; }

    /// <summary>Headers documented alongside the response.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> Headers { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Detected content type when one can be inferred from headers or language.</summary>
    public string? ContentType { get; set; }

    /// <summary>Whether this response represents an error status.</summary>
    public bool IsError { get; set; }

    /// <summary>Response body or example text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Flattened schema inferred from a JSON response body.</summary>
    public IDictionary<string, string?> BodySchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Top-level keys inferred from a JSON object response body.</summary>
    public IList<string> TopLevelKeys { get; set; } = new List<string>();

    /// <summary>Parsed JSON payload when the response body is valid JSON.</summary>
    public object? JsonBody { get; set; }

    /// <summary>First-class fields inferred from the parsed JSON payload.</summary>
    public IList<HtmlCrawlStructuredField> BodyFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
