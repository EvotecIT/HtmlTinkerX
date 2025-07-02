using System;

namespace PSParseHTML;

/// <summary>
/// Represents a &lt;meta&gt; tag with name/content attributes.
/// </summary>
public sealed class HtmlMetaTag {
    /// <summary>Name attribute value.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Content attribute value.</summary>
    public string Content { get; set; } = string.Empty;
}
