using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Result of HTML list parsing with metadata.
/// </summary>
public class HtmlListResult {
    /// <summary>
    /// Metadata describing the parsed list.
    /// </summary>
    public HtmlListMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Collection of list items grouped by list element.
    /// </summary>
    public List<List<string>> Items { get; set; } = new();
}
