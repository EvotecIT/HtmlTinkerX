namespace PSParseHTML;

/// <summary>
/// Information about an interactive element on a web page.
/// </summary>
public sealed class HtmlInteractableInfo {
    /// <summary>Index of the element in the list.</summary>
    public int Index { get; set; }

    /// <summary>Visible text for the element.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Indicates whether the element is visible.</summary>
    public bool Visible { get; set; }

    /// <summary>CSS selector identifying the element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Link target when applicable.</summary>
    public string? Href { get; set; }

    /// <summary>HTML tag name.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Element id attribute if present.</summary>
    public string? Id { get; set; }

    /// <summary>Element class attribute if present.</summary>
    public string? Class { get; set; }
}
