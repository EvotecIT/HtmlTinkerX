namespace HtmlTinkerX;

/// <summary>
/// Controls whether extraction keeps or removes obviously hidden DOM content.
/// </summary>
public enum HtmlCrawlHiddenContentMode {
    /// <summary>
    /// Remove content hidden through explicit DOM attributes or inline styles.
    /// </summary>
    RespectHidden = 0,

    /// <summary>
    /// Keep hidden DOM content in extracted HTML, text, and markdown.
    /// </summary>
    IncludeHidden = 1
}
