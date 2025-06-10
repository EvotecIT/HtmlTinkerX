namespace PSParseHTML;

/// <summary>
/// Information about an interactive element on a web page.
/// </summary>
public sealed class HtmlInteractableInfo {
    /// <summary>Index of the element in the list.</summary>
    public int Index { get; set; }

    /// <summary>Visible text for the element.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>HTML tag name.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Snippet of the outer HTML for reference.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Link target when applicable.</summary>
    public string? Href { get; set; }
}
