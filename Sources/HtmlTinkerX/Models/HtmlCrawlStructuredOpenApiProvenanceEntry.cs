namespace HtmlTinkerX;

/// <summary>
/// Single provenance entry describing one contributing source for an inferred operation.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiProvenanceEntry {
    /// <summary>URL of the page that contributed this evidence.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Kind of evidence, such as Heading, CodeSample, RequestExample, or ResponseExample.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the contributing source element.</summary>
    public string? SelectorHint { get; set; }

    /// <summary>Short label describing the specific contributing artifact.</summary>
    public string? Label { get; set; }
}
