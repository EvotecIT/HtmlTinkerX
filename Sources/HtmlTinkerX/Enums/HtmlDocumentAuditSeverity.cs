namespace HtmlTinkerX;

/// <summary>
/// Describes the impact of an HTML document audit issue.
/// </summary>
public enum HtmlDocumentAuditSeverity {
    /// <summary>The issue is informational and does not make the document invalid.</summary>
    Information,

    /// <summary>The issue should be corrected but may not block every consumer.</summary>
    Warning,

    /// <summary>The issue represents an invalid, unsafe, or inaccessible document contract.</summary>
    Error
}
