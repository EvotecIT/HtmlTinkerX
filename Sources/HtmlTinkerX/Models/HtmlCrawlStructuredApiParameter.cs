using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured API parameter inferred from docs tables near an endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredApiParameter {
    /// <summary>Parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parameter type when documented.</summary>
    public string? Type { get; set; }

    /// <summary>Parameter format when documented or inferred, for example uuid or date-time.</summary>
    public string? Format { get; set; }

    /// <summary>Parameter location such as path, query, body, or header.</summary>
    public string? Location { get; set; }

    /// <summary>Whether the parameter is required when that was documented.</summary>
    public bool? Required { get; set; }

    /// <summary>Whether the parameter may be null when that was documented or inferred.</summary>
    public bool? Nullable { get; set; }

    /// <summary>Parameter description when available.</summary>
    public string? Description { get; set; }

    /// <summary>Default value when documented.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Example value when documented.</summary>
    public string? ExampleValue { get; set; }

    /// <summary>Regex or other pattern hint when documented.</summary>
    public string? Pattern { get; set; }

    /// <summary>Allowed enum values when documented or inferred.</summary>
    public IList<string> EnumValues { get; set; } = new List<string>();

    /// <summary>Compact selector-like hint for the source parameter table.</summary>
    public string? SelectorHint { get; set; }
}
