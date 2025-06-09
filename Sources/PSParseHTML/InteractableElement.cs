namespace PSParseHTML;

/// <summary>
/// Represents a clickable or interactable element on a web page.
/// </summary>
public sealed class InteractableElement {
    /// <summary>Index of the element in the result set.</summary>
    public int Index { get; set; }

    /// <summary>Inner text or value of the element.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Tag name of the element.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>First 80 characters of the outer HTML.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Hyperlink or onclick attribute value.</summary>
    public string? Href { get; set; }
}
