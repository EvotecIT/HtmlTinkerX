using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Aggregated error information inferred for an API endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredApiError {
    /// <summary>Status code when one can be inferred from error examples or prose.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Status text when one can be inferred from error examples or prose.</summary>
    public string? StatusText { get; set; }

    /// <summary>Short summary describing the failure mode.</summary>
    public string? Summary { get; set; }

    /// <summary>Headers documented for this error family.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> Headers { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Detected content type when one can be inferred from examples.</summary>
    public string? ContentType { get; set; }

    /// <summary>Flattened schema merged from matching error examples.</summary>
    public IDictionary<string, string?> Schema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>First-class fields merged from matching error examples.</summary>
    public IList<HtmlCrawlStructuredField> Fields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Number of matching error examples that contributed to this catalog entry.</summary>
    public int SampleCount { get; set; }

    /// <summary>Compact selector-like hint for the primary source element.</summary>
    public string? SelectorHint { get; set; }
}
