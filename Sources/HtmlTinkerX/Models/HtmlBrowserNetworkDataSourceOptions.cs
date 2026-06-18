using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options for converting observed browser network traffic into browserless extraction candidates.
/// </summary>
public sealed class HtmlBrowserNetworkDataSourceOptions {
    /// <summary>Page URL used as the same-origin reference. When omitted for a session, the current page URL is used.</summary>
    public string? PageUrl { get; set; }

    /// <summary>Browser resource types considered as data-source candidates. Defaults to XHR and Fetch.</summary>
    public IList<HtmlNetworkResourceType> ResourceTypes { get; } = new List<HtmlNetworkResourceType>();

    /// <summary>Include failed or non-successful requests in the output.</summary>
    public bool IncludeFailed { get; set; }

    /// <summary>Include non-GET requests. They are classified as higher risk and are not fetched automatically.</summary>
    public bool IncludeNonGet { get; set; }

    /// <summary>Include endpoints outside the page origin.</summary>
    public bool IncludeExternal { get; set; }

    /// <summary>Copy captured response bodies into the data source when available.</summary>
    public bool IncludeResponseBody { get; set; }

    /// <summary>Maximum number of data sources returned. Zero means no limit.</summary>
    public int MaxSources { get; set; }
}
