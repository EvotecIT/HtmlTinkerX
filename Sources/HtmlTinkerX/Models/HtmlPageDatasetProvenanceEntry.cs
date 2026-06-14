namespace HtmlTinkerX;

/// <summary>
/// Provenance entry explaining which page surface contributed to a dataset chunk.
/// </summary>
public sealed class HtmlPageDatasetProvenanceEntry {
    /// <summary>Source family such as ReadableText, Data, Form, Endpoint, Token, or Warning.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best available name for the contributing item.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CSS-like selector hint for the contributing item, when available.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>URL, action, or endpoint associated with the contributing item, when available.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Additional source label such as Meta, Script, Form, InlineScript, or LinkedScript.</summary>
    public string Source { get; set; } = string.Empty;
}
