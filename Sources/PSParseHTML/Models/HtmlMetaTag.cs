using System;

namespace PSParseHTML;

/// <summary>
/// Represents an HTML <meta> tag.
/// </summary>
public class HtmlMetaTag {
    /// <summary>Name or property of the meta tag.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Value of the content attribute or charset.</summary>
    public string Content { get; set; } = string.Empty;
}
