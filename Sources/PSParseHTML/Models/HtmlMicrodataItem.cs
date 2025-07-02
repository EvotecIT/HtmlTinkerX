using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Represents a microdata item as defined by schema.org.
/// </summary>
public sealed class HtmlMicrodataItem {
    /// <summary>
    /// Value of the <c>itemid</c> attribute on the itemscope element.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Value of the <c>itemtype</c> attribute on the itemscope element.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Properties associated with the item. Each property can have multiple values.
    /// </summary>
    public Dictionary<string, List<string>> Properties { get; set; } = new();
}
