using System;

namespace HtmlTinkerX;

/// <summary>
/// Options for browserless data source discovery.
/// </summary>
public sealed class HtmlBrowserlessDiscoveryOptions {
    /// <summary>Base URL used to resolve relative endpoint and asset URLs.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Downloads same-origin linked JavaScript files and inspects them for endpoints.</summary>
    public bool IncludeLinkedScripts { get; set; }

    /// <summary>Allows linked JavaScript discovery to inspect cross-origin scripts.</summary>
    public bool IncludeExternalLinkedScripts { get; set; }

    /// <summary>Includes static structured data sources such as JSON-LD and app state.</summary>
    public bool IncludeStaticData { get; set; } = true;

    /// <summary>Includes endpoint candidates discovered in forms and JavaScript.</summary>
    public bool IncludeApiEndpoints { get; set; } = true;

    /// <summary>Returns only candidates that can be extracted directly by HtmlTinkerX.</summary>
    public bool DirectOnly { get; set; }

    /// <summary>Maximum number of candidates to return. Values below one mean no explicit limit.</summary>
    public int MaxSources { get; set; }
}
