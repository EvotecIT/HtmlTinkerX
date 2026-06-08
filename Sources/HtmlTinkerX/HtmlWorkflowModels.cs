namespace HtmlTinkerX;

/// <summary>Represents a script element selected from an HTML document.</summary>
public sealed class HtmlScriptReference {
    /// <summary>Source-order index of the script element.</summary>
    public int Index { get; set; }

    /// <summary>Script type attribute.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>External script URL, when present.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>URL resolved against the document base URL, when possible.</summary>
    public string? ResolvedUrl { get; set; }

    /// <summary>Inline script content, when present.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Whether the script type is JavaScript.</summary>
    public bool IsJavaScript { get; set; }

    /// <summary>Whether the script is an ECMAScript module.</summary>
    public bool IsModule { get; set; }

    /// <summary>Whether the script is external.</summary>
    public bool IsExternal { get; set; }
}

/// <summary>Represents a document asset reference.</summary>
public sealed class HtmlAssetReference {
    /// <summary>Source-order index of the asset reference.</summary>
    public int Index { get; set; }

    /// <summary>Asset kind, such as Script, Stylesheet, Image, Preload, Manifest, or Icon.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Element name that produced the asset.</summary>
    public string Element { get; set; } = string.Empty;

    /// <summary>Attribute that produced the URL, when applicable.</summary>
    public string Attribute { get; set; } = string.Empty;

    /// <summary>Original URL value.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolved URL value, when possible.</summary>
    public string? ResolvedUrl { get; set; }

    /// <summary>Element rel attribute, when present.</summary>
    public string? Rel { get; set; }

    /// <summary>Element type attribute, when present.</summary>
    public string? Type { get; set; }

    /// <summary>Element media attribute, when present.</summary>
    public string? Media { get; set; }

    /// <summary>Whether the asset URL parsed successfully.</summary>
    public bool IsValidUrl { get; set; }

    /// <summary>Whether the asset is inline content instead of an external URL.</summary>
    public bool IsInline { get; set; }

    /// <summary>Inline content, when present.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>Represents an HTML compatibility or accessibility finding.</summary>
public sealed class HtmlCompatibilityFinding {
    /// <summary>Stable rule identifier.</summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Finding severity.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Human-readable finding message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>CSS-like selector hint for the affected element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Affected element name.</summary>
    public string Element { get; set; } = string.Empty;

    /// <summary>Affected attribute name, when applicable.</summary>
    public string? Attribute { get; set; }

    /// <summary>Affected value, when applicable.</summary>
    public string? Value { get; set; }
}
