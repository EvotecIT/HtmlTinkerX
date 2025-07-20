using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a single Open Graph meta property.
/// </summary>
public sealed class OpenGraphProperty {
    /// <summary>Name of the property without the "og:" prefix.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Values associated with the property.</summary>
    public List<string> Values { get; } = new();
}
