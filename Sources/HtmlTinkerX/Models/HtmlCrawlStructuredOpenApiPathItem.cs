using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Compact OpenAPI-like path item derived from inferred operations.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiPathItem {
    /// <summary>Normalized path represented by this item.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Resources associated with this path.</summary>
    public IList<string> Resources { get; set; } = new List<string>();

    /// <summary>Operations keyed by lowercase HTTP method.</summary>
    public IDictionary<string, HtmlCrawlStructuredOpenApiOperation> Operations { get; set; } = new Dictionary<string, HtmlCrawlStructuredOpenApiOperation>(System.StringComparer.OrdinalIgnoreCase);
}
