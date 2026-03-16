namespace HtmlTinkerX;

/// <summary>
/// One normalized key/value entry from a structured specification table.
/// </summary>
public sealed class HtmlCrawlStructuredSpecItem {
    /// <summary>Specification field or property name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Specification value.</summary>
    public string Value { get; set; } = string.Empty;
}
