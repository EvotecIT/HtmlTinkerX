namespace HtmlTinkerX;

/// <summary>
/// Normalized item extracted from a browserless data source.
/// </summary>
public sealed class HtmlBrowserlessExtractionItem {
    /// <summary>Source-order index within the result.</summary>
    public int Index { get; set; }

    /// <summary>Source family, such as AppState, JsonLd, ApiEndpoint, or HtmlData.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best human-readable item name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional type, framework, or object kind.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON-style source path, such as $.items[0].</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Parsed value when the source was JSON or structured data.</summary>
    public object? Value { get; set; }

    /// <summary>Raw value for text or JSON-oriented consumers.</summary>
    public string RawValue { get; set; } = string.Empty;
}
