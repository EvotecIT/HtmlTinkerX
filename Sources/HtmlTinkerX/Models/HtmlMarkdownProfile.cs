namespace HtmlTinkerX;

/// <summary>
/// Controls which markdown dialect profile is used when HTML is converted to markdown text.
/// </summary>
public enum HtmlMarkdownProfile {
    /// <summary>
    /// Uses broadly compatible markdown output intended for generic markdown engines.
    /// </summary>
    Portable = 0,

    /// <summary>
    /// Uses richer OfficeIMO-flavored markdown output when downstream consumers can benefit from it.
    /// </summary>
    OfficeIMO = 1
}
