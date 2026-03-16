namespace HtmlTinkerX;

/// <summary>
/// Structured code sample extracted from selected content.
/// </summary>
public sealed class HtmlCrawlStructuredCodeSample {
    /// <summary>Short title derived from a nearby heading when available.</summary>
    public string? Title { get; set; }

    /// <summary>Nearby heading text associated with the sample when available.</summary>
    public string? Heading { get; set; }

    /// <summary>Detected programming or markup language when available.</summary>
    public string? Language { get; set; }

    /// <summary>Normalized sample kind such as code, command, curl, http, or json.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Sample code text with line breaks preserved.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Detected HTTP method when the sample represents an API request.</summary>
    public string? Method { get; set; }

    /// <summary>Detected API path when the sample represents an API request.</summary>
    public string? Path { get; set; }

    /// <summary>Compact selector-like hint for the source element.</summary>
    public string? SelectorHint { get; set; }
}
