namespace HtmlTinkerX;

/// <summary>
/// Defines one extracted field in a structured crawl JSON schema.
/// </summary>
public sealed class HtmlCrawlJsonSchemaField {
    /// <summary>Property path into the built-in structured object graph, for example <c>Metadata.Title</c>.</summary>
    public string? Path { get; set; }

    /// <summary>CSS selector used for DOM-driven extraction.</summary>
    public string? Selector { get; set; }

    /// <summary>Extraction mode. Supported values are <c>Text</c>, <c>Html</c>, <c>Markdown</c>, <c>Attribute</c>, <c>Exists</c>, and <c>Count</c>.</summary>
    public string? Mode { get; set; }

    /// <summary>DOM source. Supported values are <c>Selected</c> and <c>Page</c>.</summary>
    public string? Source { get; set; }

    /// <summary>Attribute name used when <see cref="Mode"/> is <c>Attribute</c>.</summary>
    public string? Attribute { get; set; }

    /// <summary>When true, returns all matches as an array instead of only the first match.</summary>
    public bool All { get; set; }
}
