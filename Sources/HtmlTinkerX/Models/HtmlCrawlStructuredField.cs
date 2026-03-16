using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured request or response field inferred from API documentation content.
/// </summary>
public sealed class HtmlCrawlStructuredField {
    /// <summary>Field name, typically the final segment of <see cref="Path"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Dot-separated field path relative to the request or response body.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Parent field path when this field is nested inside an object or array.</summary>
    public string? ParentPath { get; set; }

    /// <summary>Child field paths nested under this field.</summary>
    public IList<string> ChildPaths { get; set; } = new List<string>();

    /// <summary>Logical node kind such as field, object, array, or array-item.</summary>
    public string Kind { get; set; } = "field";

    /// <summary>Nesting depth relative to the request or response body root.</summary>
    public int Depth { get; set; }

    /// <summary>Field type such as string, integer, object, or array.</summary>
    public string? Type { get; set; }

    /// <summary>Field format such as uuid, date-time, email, or uri.</summary>
    public string? Format { get; set; }

    /// <summary>Whether the field is required when that can be determined.</summary>
    public bool? Required { get; set; }

    /// <summary>Whether the field may be null when that can be determined.</summary>
    public bool? Nullable { get; set; }

    /// <summary>Example value captured for the field when available.</summary>
    public string? ExampleValue { get; set; }

    /// <summary>Allowed enum values when documented or inferred.</summary>
    public IList<string> EnumValues { get; set; } = new List<string>();

    /// <summary>Origin of the field, for example ParameterTable or JsonResponse.</summary>
    public string? Source { get; set; }

    /// <summary>Detailed provenance entries explaining which rows or samples introduced the field.</summary>
    public IList<HtmlCrawlStructuredFieldProvenanceEntry> Provenance { get; set; } = new List<HtmlCrawlStructuredFieldProvenanceEntry>();

    /// <summary>Evidence count used to estimate how strongly this field is supported by the crawl.</summary>
    public int EvidenceCount { get; set; }

    /// <summary>Confidence score from 0-100 estimating how reliable this field is for downstream automation.</summary>
    public int ConfidenceScore { get; set; }
}
