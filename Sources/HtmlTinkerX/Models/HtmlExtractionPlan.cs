using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Diagnostic recommendation describing how a page should be extracted.
/// </summary>
public sealed class HtmlExtractionPlan {
    /// <summary>Recommended extraction workflow.</summary>
    public HtmlExtractionPlanMode RecommendedMode { get; set; } = HtmlExtractionPlanMode.Static;

    /// <summary>Confidence in the recommendation.</summary>
    public HtmlExtractionPlanConfidence Confidence { get; set; } = HtmlExtractionPlanConfidence.Medium;

    /// <summary>PowerShell command that can be used as a starting point.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Recommended extraction profile name for this page.</summary>
    public string SuggestedProfileName { get; set; } = string.Empty;

    /// <summary>PowerShell command that demonstrates the recommended extraction profile.</summary>
    public string SuggestedProfileCommand { get; set; } = string.Empty;

    /// <summary>Short explanation of why the profile was selected.</summary>
    public string SuggestedProfileReason { get; set; } = string.Empty;

    /// <summary>Human-readable signals that explain the recommendation.</summary>
    public IReadOnlyList<string> Reasons { get; set; } = new List<string>();

    /// <summary>Warnings about auth, sensitivity, or extraction risk.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = new List<string>();

    /// <summary>Best title discovered in the document.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Approximate word count from the readable text extractor.</summary>
    public int WordCount { get; set; }

    /// <summary>Number of script elements in the page.</summary>
    public int ScriptCount { get; set; }

    /// <summary>Number of linked script elements.</summary>
    public int ExternalScriptCount { get; set; }

    /// <summary>Number of forms in the page.</summary>
    public int FormCount { get; set; }

    /// <summary>Number of hidden form fields in the page.</summary>
    public int HiddenFieldCount { get; set; }

    /// <summary>Number of anchor links in the page.</summary>
    public int LinkCount { get; set; }

    /// <summary>Number of asset references discovered by the workflow parser.</summary>
    public int AssetCount { get; set; }

    /// <summary>Number of normalized structured data items.</summary>
    public int DataItemCount { get; set; }

    /// <summary>Number of application-state items discovered in scripts.</summary>
    public int AppStateCount { get; set; }

    /// <summary>Number of JSON-LD records discovered in scripts.</summary>
    public int JsonLdCount { get; set; }

    /// <summary>Number of OpenGraph values discovered in metadata.</summary>
    public int OpenGraphCount { get; set; }

    /// <summary>Whether the page has a single hidden-form auto-submit relay shape.</summary>
    public bool HasAutoSubmitForm { get; set; }

    /// <summary>Whether the page appears to contain a login form.</summary>
    public bool HasLoginForm { get; set; }

    /// <summary>Whether framework or application state was discovered in scripts.</summary>
    public bool HasAppState { get; set; }

    /// <summary>Whether JSON-LD, OpenGraph, microdata, or app data was discovered.</summary>
    public bool HasStructuredData { get; set; }

    /// <summary>Whether the page looks like a thin JavaScript shell that probably needs rendering.</summary>
    public bool LooksLikeJavaScriptShell { get; set; }
}
