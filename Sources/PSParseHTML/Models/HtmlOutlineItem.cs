using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Represents a heading within an HTML document outline.
/// </summary>
public sealed class HtmlOutlineItem {
    /// <summary>Heading text content.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Heading level (1-6).</summary>
    public int Level { get; set; }

    /// <summary>Value of the <c>id</c> attribute if present.</summary>
    public string? Id { get; set; }

    /// <summary>Child headings nested under this heading.</summary>
    public List<HtmlOutlineItem> Children { get; } = new();
}
