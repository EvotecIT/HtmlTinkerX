namespace HtmlTinkerX;

/// <summary>
/// Represents an anchor discovered in HTML together with nearby human-readable context.
/// </summary>
public sealed class HtmlDiscoveredLink {
    /// <summary>Resolved URL when a base URI was provided, otherwise the raw href.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Raw href attribute value from the anchor.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Anchor text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Anchor title attribute when provided.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Nearby parent text useful for attachment context.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>True when the resolved URL points outside the base URI host.</summary>
    public bool IsExternal { get; set; }
}
