using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents Open Graph metadata extracted from HTML.
/// </summary>
public sealed class HtmlOpenGraph {
    /// <summary>Collection of Open Graph properties.</summary>
    public List<OpenGraphProperty> Properties { get; set; } = new();
}