using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Metadata about a parsed HTML list.
/// </summary>
public class HtmlListMetadata {
    /// <summary>
    /// Index of the list element within the document.
    /// </summary>
    public int ListIndex { get; set; }

    /// <summary>
    /// Value of the id attribute on the list element.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Indicates whether the list element is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// CSS classes applied to the list element.
    /// </summary>
    public string? Classes { get; set; }

    /// <summary>
    /// Additional attributes found on the list element.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// True if the list is ordered (<c>&lt;ol&gt;</c>).
    /// </summary>
    public bool IsOrdered { get; set; }

    /// <summary>
    /// Number of items contained in the list.
    /// </summary>
    public int ItemCount { get; set; }
}
