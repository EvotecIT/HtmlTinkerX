using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options for browserless extraction.
/// </summary>
public sealed class HtmlBrowserlessExtractionOptions {
    /// <summary>Allows extraction to issue an HTTP GET for endpoint sources.</summary>
    public bool AllowHttpFetch { get; set; }

    /// <summary>Allows medium-risk endpoint sources when HTTP fetch is enabled.</summary>
    public bool AllowMediumRiskEndpoints { get; set; }

    /// <summary>Allows external-origin endpoint sources when HTTP fetch is enabled.</summary>
    public bool AllowExternalEndpoints { get; set; }

    /// <summary>Includes raw payload or response content in the result.</summary>
    public bool IncludeRawContent { get; set; }

    /// <summary>Maximum response body size to keep from direct HTTP endpoint extraction.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>Optional request headers used when direct HTTP extraction is explicitly enabled.</summary>
    public IReadOnlyDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
}
