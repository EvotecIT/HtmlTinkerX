using System;
using global::AngleSharp;

namespace PSParseHTML;

/// <summary>
/// Options controlling PreMailer processing.
/// </summary>
public class PreMailerOptions {
    /// <summary>Base URL for resolving relative CSS links.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Removes <style> elements after inlining.</summary>
    public bool RemoveStyleElements { get; set; }

    /// <summary>CSS selector of elements to ignore when inlining.</summary>
    public string? IgnoreElements { get; set; }

    /// <summary>CSS content to inline.</summary>
    public string? Css { get; set; }

    /// <summary>Optional path to a CSS file to inline.</summary>
    public string? CssFilePath { get; set; }

    /// <summary>Strip id and class attributes from output.</summary>
    public bool StripIdAndClassAttributes { get; set; }

    /// <summary>Remove HTML and CSS comments.</summary>
    public bool RemoveComments { get; set; }

    /// <summary>Formatter used for generating HTML output.</summary>
    public global::AngleSharp.IMarkupFormatter? CustomFormatter { get; set; }

    /// <summary>Preserve media queries from style nodes.</summary>
    public bool PreserveMediaQueries { get; set; }

    /// <summary>Use email formatter when generating HTML.</summary>
    public bool UseEmailFormatter { get; set; }

    // Analytics configuration
    /// <summary>Add Google Analytics tags.</summary>
    public bool AddAnalyticsTags { get; set; }

    public string? AnalyticsSource { get; set; }
    public string? AnalyticsMedium { get; set; }
    public string? AnalyticsCampaign { get; set; }
    public string? AnalyticsContent { get; set; }
    public string? AnalyticsDomain { get; set; }
}
