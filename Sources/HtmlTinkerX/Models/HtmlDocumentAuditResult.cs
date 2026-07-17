using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Contains reusable correctness, safety, and accessibility findings for one HTML document.
/// </summary>
public sealed class HtmlDocumentAuditResult {
    /// <summary>Issues discovered in document order.</summary>
    public IReadOnlyList<HtmlDocumentAuditIssue> Issues { get; set; } = Array.Empty<HtmlDocumentAuditIssue>();

    /// <summary>Whether the audit completed without error-severity issues.</summary>
    public bool IsValid => !Issues.Any(static issue => issue.Severity == HtmlDocumentAuditSeverity.Error);

    /// <summary>Number of error-severity issues.</summary>
    public int ErrorCount => Issues.Count(static issue => issue.Severity == HtmlDocumentAuditSeverity.Error);

    /// <summary>Number of warning-severity issues.</summary>
    public int WarningCount => Issues.Count(static issue => issue.Severity == HtmlDocumentAuditSeverity.Warning);
}
