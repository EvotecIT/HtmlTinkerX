namespace HtmlTinkerX;

/// <summary>
/// Defines how one property is extracted relative to a selected HTML item.
/// </summary>
public sealed class HtmlDomFieldDefinition {
    /// <summary>CSS selector evaluated relative to each selected item. An empty selector reads the item itself.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Attribute to read instead of element text.</summary>
    public string? Attribute { get; set; }

    /// <summary>Value kind used when no attribute is specified. Supported values are Text and Html.</summary>
    public string ValueKind { get; set; } = "Text";

    /// <summary>Return every matching value instead of only the first.</summary>
    public bool All { get; set; }

    /// <summary>Throw when no matching value exists for an item.</summary>
    public bool Required { get; set; }

    /// <summary>Value returned when the selector or attribute does not produce a value.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Resolve relative URL attributes against the document or caller-provided base URL.</summary>
    public bool ResolveUrl { get; set; }
}
