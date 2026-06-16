namespace HtmlTinkerX;

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

    /// <summary>
    /// True when the element or one of its ancestors has attributes or styles
    /// that may hide it from assistive technologies.
    /// </summary>
    public bool PotentiallyHidden { get; set; }

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

    /// <summary>Whether the element is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether the element is editable.</summary>
    public bool Editable { get; set; }

    /// <summary>Whether the element intersects the viewport.</summary>
    public bool InViewport { get; set; }

    /// <summary>Element left coordinate relative to the viewport.</summary>
    public double X { get; set; }

    /// <summary>Element top coordinate relative to the viewport.</summary>
    public double Y { get; set; }

    /// <summary>Element width in CSS pixels.</summary>
    public double Width { get; set; }

    /// <summary>Element height in CSS pixels.</summary>
    public double Height { get; set; }
}
