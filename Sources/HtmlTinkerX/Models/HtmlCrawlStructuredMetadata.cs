using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Common page-level metadata promoted for easier downstream ingestion.
/// </summary>
public sealed class HtmlCrawlStructuredMetadata {
    /// <summary>Page title derived from the document title or equivalent metadata.</summary>
    public string? Title { get; set; }

    /// <summary>Short page description from metadata when available.</summary>
    public string? Description { get; set; }

    /// <summary>Canonical URL discovered in markup, when available.</summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>Document language when declared on the root HTML element.</summary>
    public string? Language { get; set; }

    /// <summary>Open Graph or application site name when available.</summary>
    public string? SiteName { get; set; }

    /// <summary>Open Graph page type when available.</summary>
    public string? Type { get; set; }

    /// <summary>Author metadata when available.</summary>
    public string? Author { get; set; }

    /// <summary>Published timestamp metadata when available.</summary>
    public string? PublishedTime { get; set; }

    /// <summary>Modified timestamp metadata when available.</summary>
    public string? ModifiedTime { get; set; }

    /// <summary>Robots metadata when available.</summary>
    public string? Robots { get; set; }

    /// <summary>Generator metadata when available.</summary>
    public string? Generator { get; set; }

    /// <summary>Primary image URL when available.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Keywords metadata split into normalized items.</summary>
    public IList<string> Keywords { get; set; } = new List<string>();

    /// <summary>Total number of collected meta tags.</summary>
    public int MetaTagCount { get; set; }

    /// <summary>Total number of collected Open Graph properties.</summary>
    public int OpenGraphPropertyCount { get; set; }
}
