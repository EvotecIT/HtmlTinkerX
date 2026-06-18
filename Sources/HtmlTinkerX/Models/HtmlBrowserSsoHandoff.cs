using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// SSO protocol handoff observed in the current browser page, either as a form or URL query/fragment callback.
/// </summary>
public sealed class HtmlBrowserSsoHandoff {
    /// <summary>Zero-based form index on the page.</summary>
    public int Index { get; set; }

    /// <summary>Protocol family inferred from the form fields.</summary>
    public HtmlBrowserSsoHandoffKind Kind { get; set; } = HtmlBrowserSsoHandoffKind.Unknown;

    /// <summary>Current browser page URL.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Current browser page title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Best-effort CSS selector for the form.</summary>
    public string FormSelector { get; set; } = string.Empty;

    /// <summary>Resolved form action URL.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Form HTTP method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Whether HtmlTinkerX's SSO auto-submit guard prevented this form from submitting.</summary>
    public bool AutoSubmitPrevented { get; set; }

    /// <summary>Whether any field in the form is considered sensitive authentication material.</summary>
    public bool ContainsSensitiveValues { get; set; }

    /// <summary>Fields collected from the form.</summary>
    public List<HtmlBrowserSsoField> Fields { get; set; } = new();

    /// <summary>Form fields keyed by input name, using the same redaction and truncation policy as <see cref="Fields"/>.</summary>
    public Dictionary<string, string> FormData { get; set; } = new();

    /// <summary>Copy-ready PowerShell pattern for replaying the captured handoff with Invoke-WebRequest.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Operator guidance about redaction, truncation, duplicate fields, or one-time SSO replay risks.</summary>
    public List<string> Warnings { get; set; } = new();
}
