namespace HtmlTinkerX;

/// <summary>
/// Structured HTTP header inferred from request or response examples.
/// </summary>
public sealed class HtmlCrawlStructuredHttpHeader {
    /// <summary>Header name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Header value when one was documented.</summary>
    public string? Value { get; set; }
}
