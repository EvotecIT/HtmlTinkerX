using System;

namespace HtmlTinkerX;

/// <summary>
/// Defines how one property is extracted relative to a selected HTML item.
/// </summary>
public sealed class HtmlDomFieldDefinition {
    /// <summary>CSS selector evaluated relative to each selected item. An empty selector reads the item itself.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Attribute to read instead of element text.</summary>
    public string? Attribute { get; set; }

    /// <summary>Value kind used when no attribute is specified. Supported values are Text and Html.</summary>
    public string ValueKind { get; set; } = "Text";

    /// <summary>
    /// Optional output type: string, int, long, decimal, bool, DateTimeOffset, or an enum.
    /// Nullable value types are accepted. Null preserves the extracted value without conversion.
    /// Enums accept declared member names, ignoring case. Invalid values throw FormatException.
    /// </summary>
    public Type? DataType { get; set; }

    /// <summary>
    /// Culture name used for numeric and date conversion, such as pl-PL. Null or empty uses
    /// invariant culture. Dates without an offset use UTC; explicit offsets are preserved.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>Treat empty or whitespace-only values as missing when applying Required and DefaultValue.</summary>
    public bool TreatEmptyAsMissing { get; set; }

    /// <summary>Optional minimum number of non-missing values before selecting the first or all values.</summary>
    public int? MinimumValueCount { get; set; }

    /// <summary>Optional maximum number of non-missing values before selecting the first or all values.</summary>
    public int? MaximumValueCount { get; set; }

    /// <summary>Return every matching value instead of only the first.</summary>
    public bool All { get; set; }

    /// <summary>Throw when no matching value exists for an item.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Value returned for an optional single-value field when no value is found. Converted with
    /// DataType when specified. Required fields still throw; All fields return an empty array.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>Resolve relative URL attributes against the document or caller-provided base URL.</summary>
    public bool ResolveUrl { get; set; }
}