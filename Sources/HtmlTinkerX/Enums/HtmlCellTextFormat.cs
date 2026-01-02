namespace HtmlTinkerX;

/// <summary>
/// Controls how text is extracted from table cells.
/// </summary>
public enum HtmlCellTextFormat {
    /// <summary>
    /// Single-line, collapses whitespace (legacy/default).
    /// </summary>
    Compact = 0,
    /// <summary>
    /// Preserves block boundaries and list items with new-lines and bullets.
    /// </summary>
    Lines = 1,
    /// <summary>
    /// Similar to Lines but uses Markdown-friendly bullets.
    /// </summary>
    Markdown = 2
}
