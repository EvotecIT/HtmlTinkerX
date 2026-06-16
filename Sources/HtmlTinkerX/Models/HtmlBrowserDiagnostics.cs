using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Describes browser runtime, storage, and observed network signals for an active page.
/// </summary>
public sealed class HtmlBrowserDiagnostics {
    /// <summary>Current page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Current page title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Browser user agent reported by <c>navigator.userAgent</c>.</summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>Browser language reported by <c>navigator.language</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Browser platform reported by <c>navigator.platform</c>.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Whether <c>navigator.webdriver</c> is set.</summary>
    public bool WebDriver { get; set; }

    /// <summary>Whether browser cookies are enabled.</summary>
    public bool CookiesEnabled { get; set; }

    /// <summary>Whether the page reports itself online.</summary>
    public bool Online { get; set; }

    /// <summary>Current viewport width.</summary>
    public int ViewportWidth { get; set; }

    /// <summary>Current viewport height.</summary>
    public int ViewportHeight { get; set; }

    /// <summary>Current device pixel ratio.</summary>
    public double DevicePixelRatio { get; set; }

    /// <summary>Timezone resolved by the browser JavaScript runtime.</summary>
    public string Timezone { get; set; } = string.Empty;

    /// <summary>Keys present in local storage.</summary>
    public IReadOnlyList<string> LocalStorageKeys { get; set; } = System.Array.Empty<string>();

    /// <summary>Keys present in session storage.</summary>
    public IReadOnlyList<string> SessionStorageKeys { get; set; } = System.Array.Empty<string>();

    /// <summary>Number of cookies available to the browser context.</summary>
    public int CookieCount { get; set; }

    /// <summary>Total captured network entries in the current session.</summary>
    public int NetworkEntryCount { get; set; }

    /// <summary>Observed XHR and Fetch calls.</summary>
    public IReadOnlyList<HtmlNetworkEntry> ObservedApiCalls { get; set; } = System.Array.Empty<HtmlNetworkEntry>();

    /// <summary>Observed failed or blocked requests.</summary>
    public IReadOnlyList<HtmlNetworkEntry> FailedRequests { get; set; } = System.Array.Empty<HtmlNetworkEntry>();

    /// <summary>Observed WebSocket connections.</summary>
    public IReadOnlyList<HtmlNetworkEntry> WebSocketEntries { get; set; } = System.Array.Empty<HtmlNetworkEntry>();

    /// <summary>Observed console errors.</summary>
    public IReadOnlyList<HtmlConsoleEntry> ConsoleErrors { get; set; } = System.Array.Empty<HtmlConsoleEntry>();

    /// <summary>Warnings about browser/runtime consistency signals that may affect extraction reliability.</summary>
    public IReadOnlyList<string> ConsistencyWarnings { get; set; } = System.Array.Empty<string>();

    /// <summary>Simple 0-100 diagnostic score based on browser/runtime inconsistency signals.</summary>
    public int FingerprintRiskScore { get; set; }
}
