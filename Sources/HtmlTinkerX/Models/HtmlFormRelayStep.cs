using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Diagnostic information for one browserless form relay step.
/// </summary>
public sealed class HtmlFormRelayStep {
    /// <summary>Zero-based relay step index.</summary>
    public int Index { get; set; }

    /// <summary>Submitted form method.</summary>
    public FormMethod Method { get; set; } = FormMethod.Get;

    /// <summary>Resolved form action URL.</summary>
    public string ActionUrl { get; set; } = string.Empty;

    /// <summary>Field names submitted by this step. Values are intentionally omitted.</summary>
    public IReadOnlyList<string> FieldNames { get; set; } = new List<string>();

    /// <summary>Protocol family inferred from field names.</summary>
    public HtmlFormRelayProtocolHint ProtocolHint { get; set; } = HtmlFormRelayProtocolHint.Generic;

    /// <summary>Whether this step crosses from the current host to another host.</summary>
    public bool IsCrossHost { get; set; }

    /// <summary>Whether this step crosses scheme, host, or port from the current response.</summary>
    public bool IsCrossOrigin { get; set; }

    /// <summary>HTTP status code returned by the step, when submitted.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Final response URL for the step, after redirects if available.</summary>
    public string ResponseUrl { get; set; } = string.Empty;

    /// <summary>Whether the step was blocked before submission.</summary>
    public bool Blocked { get; set; }
}
