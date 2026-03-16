namespace HtmlTinkerX;

/// <summary>
/// Provenance metadata for a structured request or response field.
/// </summary>
public sealed class HtmlCrawlStructuredFieldProvenanceEntry {
    /// <summary>URL of the page that contributed this field evidence.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Kind of source that introduced this field, such as ParameterTable or JsonResponse.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Compact selector-like hint for the contributing source element.</summary>
    public string? SelectorHint { get; set; }

    /// <summary>Short label describing the contributing row or sample.</summary>
    public string? Label { get; set; }
}
