using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Lightweight page-level API catalog built from inferred endpoints.
/// </summary>
public sealed class HtmlCrawlStructuredApiCatalog {
    /// <summary>Short title for the catalog, typically from page metadata.</summary>
    public string? Title { get; set; }

    /// <summary>Short description for the catalog, typically from page metadata.</summary>
    public string? Description { get; set; }

    /// <summary>Total number of operations inferred on the page.</summary>
    public int OperationCount { get; set; }

    /// <summary>Total number of unique paths inferred on the page.</summary>
    public int PathCount { get; set; }

    /// <summary>Total number of operations that require authentication.</summary>
    public int AuthenticatedOperationCount { get; set; }

    /// <summary>Total number of operations with rate-limit hints.</summary>
    public int RateLimitedOperationCount { get; set; }

    /// <summary>Total number of aggregated documented error families.</summary>
    public int ErrorCatalogCount { get; set; }

    /// <summary>Distinct resources inferred from endpoint paths.</summary>
    public IList<string> Resources { get; set; } = new List<string>();

    /// <summary>Distinct tags inferred from endpoint paths and context.</summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>Stable operation identifiers inferred for page endpoints.</summary>
    public IList<string> OperationIds { get; set; } = new List<string>();

    /// <summary>Distinct normalized paths inferred on the page.</summary>
    public IList<string> Paths { get; set; } = new List<string>();
}
