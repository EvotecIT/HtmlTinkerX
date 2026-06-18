using HtmlTinkerX;
using System.Collections;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Describes common browser launch parameters supplied by PowerShell cmdlets.
/// </summary>
internal sealed class HtmlBrowserLaunchOptionRequest {
    /// <summary>Bound PowerShell parameters for the invoking cmdlet.</summary>
    public IDictionary BoundParameters { get; set; } = new Hashtable();

    /// <summary>Optional launch options to use as the starting point before profile and explicit parameter values are applied.</summary>
    public HtmlBrowserLaunchOptions? BaseOptions { get; set; }

    /// <summary>Optional browser profile JSON file used as launch defaults.</summary>
    public string? ProfilePath { get; set; }

    /// <summary>Intent-focused browser scenario requested by the caller.</summary>
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Browser engine requested by the caller.</summary>
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force browser runtime reinstall before launch.</summary>
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    public SwitchParameter Visible { get; set; }

    /// <summary>Delay Playwright actions by the specified milliseconds.</summary>
    public int SlowMo { get; set; }

    /// <summary>Timeout in milliseconds for navigation and browser operations.</summary>
    public int Timeout { get; set; } = 10000;

    /// <summary>PowerShell parameter name that controls <see cref="Timeout"/>.</summary>
    public string TimeoutParameterName { get; set; } = nameof(Timeout);

    /// <summary>Initial browser navigation readiness state.</summary>
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Persistent browser user-data directory.</summary>
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file.</summary>
    public string? StatePath { get; set; }

    /// <summary>Browser distribution channel.</summary>
    public string? BrowserChannel { get; set; }

    /// <summary>Path to a browser executable.</summary>
    public string? BrowserExecutablePath { get; set; }

    /// <summary>Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.</summary>
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Additional browser command-line arguments.</summary>
    public string[] BrowserArgument { get; set; } = System.Array.Empty<string>();

    /// <summary>Enable Chromium sandboxing when supported.</summary>
    public SwitchParameter ChromiumSandbox { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the proxy server.</summary>
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User agent string used by the browser context.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Locale used by the browser context.</summary>
    public string? Locale { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    public int? ViewportHeight { get; set; }

    /// <summary>Screen width in pixels.</summary>
    public int? ScreenWidth { get; set; }

    /// <summary>Screen height in pixels.</summary>
    public int? ScreenHeight { get; set; }

    /// <summary>Scaling factor for high DPI devices.</summary>
    public double? DeviceScaleFactor { get; set; }

    /// <summary>Expose mobile browser behavior where supported.</summary>
    public SwitchParameter Mobile { get; set; }

    /// <summary>Expose touch input where supported.</summary>
    public SwitchParameter Touch { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier used by the browser JavaScript runtime.</summary>
    public string? Timezone { get; set; }

    /// <summary>Browser permissions granted to pages in the context.</summary>
    public string[] Permission { get; set; } = System.Array.Empty<string>();

    /// <summary>JavaScript snippets evaluated before page scripts run.</summary>
    public string[] InitScript { get; set; } = System.Array.Empty<string>();

    /// <summary>JavaScript files evaluated before page scripts run.</summary>
    public string[] InitScriptPath { get; set; } = System.Array.Empty<string>();

    /// <summary>Browser resource types to abort before navigation.</summary>
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation.</summary>
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();
}
