namespace HtmlTinkerX;

/// <summary>
/// Describes one issue discovered while auditing an HTML document.
/// </summary>
public sealed class HtmlDocumentAuditIssue {
    /// <summary>Stable machine-readable issue code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Impact of the issue.</summary>
    public HtmlDocumentAuditSeverity Severity { get; set; }

    /// <summary>Human-readable explanation of the issue.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Compact selector-like description of the affected element, when applicable.</summary>
    public string Element { get; set; } = string.Empty;
}
