using global::AngleSharp;
using System;
using System.Net.Http;

namespace HtmlTinkerX;

/// <summary>
/// Options controlling PreMailer processing.
/// </summary>
public class PreMailerOptions {
    /// <summary>Base URL for resolving relative CSS links.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Removes <c>&lt;style&gt;</c> elements after inlining.</summary>
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

    /// <summary>
    /// When enabled, CSS from <c>&lt;link&gt;</c> tags will be downloaded and inlined.
    /// </summary>
    public bool DownloadRemoteCss { get; set; }

    /// <summary>
    /// Optional HTTP client used to download linked stylesheets. The caller retains ownership of the client.
    /// When omitted, <see cref="HtmlHttpClientFactory.Shared"/> is used.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    // Analytics configuration
    /// <summary>Add Google Analytics tags.</summary>
    public bool AddAnalyticsTags { get; set; }

    /// <summary>UTM source parameter value.</summary>
    public string? AnalyticsSource { get; set; }
    /// <summary>UTM medium parameter value.</summary>
    public string? AnalyticsMedium { get; set; }
    /// <summary>UTM campaign parameter value.</summary>
    public string? AnalyticsCampaign { get; set; }
    /// <summary>UTM content parameter value.</summary>
    public string? AnalyticsContent { get; set; }
    /// <summary>Domain used when constructing analytics links.</summary>
    public string? AnalyticsDomain { get; set; }
}
