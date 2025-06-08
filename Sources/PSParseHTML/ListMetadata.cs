using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Metadata about a parsed HTML list.
/// </summary>
public class ListMetadata {
    public int ListIndex { get; set; }
    public string? Id { get; set; }
    public string? Classes { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public bool IsOrdered { get; set; }
    public int ItemCount { get; set; }
    public bool IsVisible { get; set; } = true;
}
