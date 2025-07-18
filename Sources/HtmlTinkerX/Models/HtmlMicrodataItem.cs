using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a microdata item extracted from HTML.
/// </summary>
public sealed class HtmlMicrodataItem {
    /// <summary>Value of the itemtype attribute.</summary>
    public string? Type { get; set; }

    /// <summary>Value of the itemid attribute.</summary>
    public string? Id { get; set; }

    /// <summary>Properties extracted from the item.</summary>
    public Dictionary<string, List<string>> Properties { get; set; } = new();
}
