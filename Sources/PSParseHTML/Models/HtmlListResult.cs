using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Result of HTML list parsing with metadata.
/// </summary>
public class HtmlListResult {
    public HtmlListMetadata Metadata { get; set; } = new();
    public List<List<string>> Items { get; set; } = new();
}
