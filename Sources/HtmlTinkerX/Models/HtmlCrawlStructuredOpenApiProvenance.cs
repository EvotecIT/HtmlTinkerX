using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Provenance metadata explaining where an inferred OpenAPI-like operation came from.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiProvenance {
    /// <summary>Distinct source pages that contributed to the operation.</summary>
    public IList<string> PageUrls { get; set; } = new List<string>();

    /// <summary>Distinct source kinds that contributed to the operation, such as Heading or CodeSample.</summary>
    public IList<string> SourceKinds { get; set; } = new List<string>();

    /// <summary>Detailed provenance entries for debugging and trust inspection.</summary>
    public IList<HtmlCrawlStructuredOpenApiProvenanceEntry> Entries { get; set; } = new List<HtmlCrawlStructuredOpenApiProvenanceEntry>();
}
