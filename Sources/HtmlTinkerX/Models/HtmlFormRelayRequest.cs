using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Parsed hidden-form relay request.
/// </summary>
public sealed class HtmlFormRelayRequest {
    /// <summary>Resolved form action URL.</summary>
    public Uri ActionUri { get; set; } = null!;

    /// <summary>Form submission method.</summary>
    public FormMethod Method { get; set; } = FormMethod.Get;

    /// <summary>Submitted fields keyed by name. Repeated field names keep the last value for dictionary compatibility; use <see cref="FieldValues"/> for browser-faithful submissions.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();

    /// <summary>Submitted fields in source order, preserving repeated field names.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> FieldValues { get; set; } = new List<KeyValuePair<string, string>>();

    /// <summary>Names of fields that will be submitted.</summary>
    public IReadOnlyList<string> FieldNames { get; set; } = new List<string>();

    /// <summary>Protocol family inferred from field names.</summary>
    public HtmlFormRelayProtocolHint ProtocolHint { get; set; } = HtmlFormRelayProtocolHint.Generic;

    /// <summary>Whether script or markup indicated automatic form submission.</summary>
    public bool HasAutoSubmitMarker { get; set; }
}
