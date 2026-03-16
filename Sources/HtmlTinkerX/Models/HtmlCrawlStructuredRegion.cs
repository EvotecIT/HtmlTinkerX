using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Lightweight summary of a detected page region such as navigation or footer.
/// </summary>
public sealed class HtmlCrawlStructuredRegion {
    /// <summary>Semantic region kind.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>HTML tag name of the detected region.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Element ID when available.</summary>
    public string? Id { get; set; }

    /// <summary>Element class names when available.</summary>
    public IList<string> Classes { get; set; } = new List<string>();

    /// <summary>Compact selector-like hint for the detected element.</summary>
    public string? SelectorHint { get; set; }

    /// <summary>Element role when available.</summary>
    public string? Role { get; set; }

    /// <summary>ARIA label when available.</summary>
    public string? AriaLabel { get; set; }

    /// <summary>Normalized short summary of the region text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Word count of the detected region.</summary>
    public int WordCount { get; set; }

    /// <summary>Anchor count of the detected region.</summary>
    public int LinkCount { get; set; }

    /// <summary>Heading count of the detected region.</summary>
    public int HeadingCount { get; set; }

    /// <summary>Distinct link labels captured from the region.</summary>
    public IList<string> LinkLabels { get; set; } = new List<string>();

    /// <summary>Whether the region looks like boilerplate or chrome.</summary>
    public bool IsLikelyBoilerplate { get; set; }
}
