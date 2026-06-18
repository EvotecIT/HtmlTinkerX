namespace HtmlTinkerX;

using System;
using System.Collections.Generic;

/// <summary>
/// Result of a browser evidence export.
/// </summary>
public sealed class HtmlBrowserEvidenceResult {
    /// <summary>Output directory containing the evidence artifacts.</summary>
    public string OutFolder { get; set; } = string.Empty;

    /// <summary>Manifest file path when a manifest was written.</summary>
    public string? ManifestPath { get; set; }

    /// <summary>Original URL requested by the caller when known.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Final browser URL after navigation and redirects.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Browser page title at capture time.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>UTC timestamp for the capture.</summary>
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>Purpose of the evidence bundle, such as Evidence or FailureEvidence.</summary>
    public string Purpose { get; set; } = "Evidence";

    /// <summary>Logical browser operation that produced this evidence when known.</summary>
    public string? Operation { get; set; }

    /// <summary>Exception type captured for failure evidence.</summary>
    public string? ErrorType { get; set; }

    /// <summary>Exception message captured for failure evidence.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Persistent browser profile path when the session uses one.</summary>
    public string? UserDataDirectory { get; set; }

    /// <summary>Whether the browser session uses a persistent profile.</summary>
    public bool IsPersistent { get; set; }

    /// <summary>Whether text artifacts and manifest URLs were redacted for common sensitive values.</summary>
    public bool Redacted { get; set; }

    /// <summary>Number of SSO handoff forms summarized when an SSO handoff summary artifact was requested.</summary>
    public int? SsoHandoffCount { get; set; }

    /// <summary>Number of locator suggestions written for failure evidence.</summary>
    public int? LocatorSuggestionCount { get; set; }

    /// <summary>Artifact files produced by the export.</summary>
    public List<HtmlBrowserEvidenceArtifact> Artifacts { get; set; } = new();
}
