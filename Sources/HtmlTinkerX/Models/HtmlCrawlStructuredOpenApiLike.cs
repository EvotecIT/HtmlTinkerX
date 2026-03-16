using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Compact OpenAPI-like snapshot derived from inferred page endpoints.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiLike {
    /// <summary>Short title for the API snapshot, typically from page metadata.</summary>
    public string? Title { get; set; }

    /// <summary>Short description for the API snapshot, typically from page metadata.</summary>
    public string? Description { get; set; }

    /// <summary>Distinct server origins inferred from the crawled page.</summary>
    public IList<string> Servers { get; set; } = new List<string>();

    /// <summary>Distinct tags inferred across operations.</summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>Distinct resources inferred across operations.</summary>
    public IList<string> Resources { get; set; } = new List<string>();

    /// <summary>Reusable component definitions deduped across operations.</summary>
    public HtmlCrawlStructuredOpenApiComponents Components { get; set; } = new();

    /// <summary>Minimum score required before an inferred operation is promoted into the strict OpenAPI export.</summary>
    public int StrictOpenApiPromotionThreshold { get; set; }

    /// <summary>Number of operations promoted into the strict OpenAPI export.</summary>
    public int StrictOpenApiEligibleOperationCount { get; set; }

    /// <summary>Number of operations kept only in the richer OpenAPI-like export because they were too weak for strict promotion.</summary>
    public int StrictOpenApiSkippedOperationCount { get; set; }

    /// <summary>Average promotion score across inferred operations.</summary>
    public double StrictOpenApiAverageScore { get; set; }

    /// <summary>Path items grouped by normalized path.</summary>
    public IDictionary<string, HtmlCrawlStructuredOpenApiPathItem> Paths { get; set; } = new Dictionary<string, HtmlCrawlStructuredOpenApiPathItem>(System.StringComparer.OrdinalIgnoreCase);
}
