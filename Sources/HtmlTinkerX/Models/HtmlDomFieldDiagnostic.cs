namespace HtmlTinkerX;

/// <summary>Selector provenance and quality outcome for one field, without its raw input.</summary>
public sealed class HtmlDomFieldDiagnostic {
    /// <summary>Zero-based index of the selected item.</summary>
    public int ItemIndex { get; set; }
    /// <summary>Caller-provided property name.</summary>
    public string PropertyName { get; set; } = string.Empty;
    /// <summary>CSS selector relative to the item; empty means the item itself.</summary>
    public string Selector { get; set; } = string.Empty;
    /// <summary>Selected attribute, or null when reading text or HTML.</summary>
    public string? Attribute { get; set; }
    /// <summary>Selected text or HTML representation when no attribute is used.</summary>
    public string ValueKind { get; set; } = "Text";
    /// <summary>Requested output type name, or null when conversion is not requested.</summary>
    public string? DataType { get; set; }
    /// <summary>Culture used for conversion; an empty name means invariant culture.</summary>
    public string Culture { get; set; } = string.Empty;
    /// <summary>Number of elements matching the selector.</summary>
    public int MatchCount { get; set; }
    /// <summary>Number of values remaining after absent attributes and optional blank filtering.</summary>
    public int ValueCount { get; set; }
    /// <summary>Declared lower value-count bound, when supplied.</summary>
    public int? MinimumValueCount { get; set; }
    /// <summary>Declared upper value-count bound, when supplied.</summary>
    public int? MaximumValueCount { get; set; }
    /// <summary>Observed extraction outcome.</summary>
    public HtmlDomFieldStatus Status { get; set; }
    /// <summary>Reason for a data error, without echoing the raw value.</summary>
    public string? Error { get; set; }
    /// <summary>Whether the field satisfies its declared required, count, and conversion rules.</summary>
    public bool IsValid => Status == HtmlDomFieldStatus.Extracted
        || Status == HtmlDomFieldStatus.Defaulted || Status == HtmlDomFieldStatus.Missing;
}