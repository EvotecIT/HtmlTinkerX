using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured specification-style table extracted from selected content.
/// </summary>
public sealed class HtmlCrawlStructuredSpecTable {
    /// <summary>Table index within the selected content.</summary>
    public int TableIndex { get; set; }

    /// <summary>Optional caption or nearby heading describing the table.</summary>
    public string? Title { get; set; }

    /// <summary>Detected table headers.</summary>
    public IList<string> Headers { get; set; } = new List<string>();

    /// <summary>Normalized key/value entries extracted from the table.</summary>
    public IList<HtmlCrawlStructuredSpecItem> Entries { get; set; } = new List<HtmlCrawlStructuredSpecItem>();

    /// <summary>Flattened key/value map for simple ingestion.</summary>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Compact selector-like hint for the source table element.</summary>
    public string? SelectorHint { get; set; }
}
