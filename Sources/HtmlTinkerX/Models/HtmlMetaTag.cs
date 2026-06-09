using System;

namespace HtmlTinkerX;

/// <summary>
/// Represents a &lt;meta&gt; tag with name/content attributes.
/// </summary>
public sealed class HtmlMetaTag {
    /// <summary>Name attribute value.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Attribute that supplied <see cref="Name"/>, such as name, property, itemprop, or http-equiv.</summary>
    public string SourceAttribute { get; set; } = string.Empty;

    /// <summary>Content attribute value.</summary>
    public string Content { get; set; } = string.Empty;
}
