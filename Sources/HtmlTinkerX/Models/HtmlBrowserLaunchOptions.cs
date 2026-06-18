namespace HtmlTinkerX;

using System;
using System.Collections.Generic;

/// <summary>
/// Describes how HtmlTinkerX should launch and prepare a browser session.
/// </summary>
/// <remarks>
/// This options object is the reusable core surface for browser automation scenarios. Cmdlets
/// should map parameters into this model instead of duplicating launch, profile, and emulation
/// rules in PowerShell-specific code.
/// </remarks>
public sealed class HtmlBrowserLaunchOptions {
    /// <summary>Browser engine to use.</summary>
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Intent-focused browser automation scenario applied to this launch configuration.</summary>
    public HtmlBrowserScenario Scenario { get; private set; } = HtmlBrowserScenario.Custom;

    /// <summary>Force re-download of browser runtimes before launch.</summary>
    public bool Clean { get; set; }

    /// <summary>Username for HTTP authentication or form login.</summary>
    public string? Username { get; set; }

    /// <summary>Password for HTTP authentication or form login.</summary>
    public string? Password { get; set; }

    /// <summary>Form-login configuration used before navigating to the requested URL.</summary>
    public HtmlFormLogin? FormLogin { get; set; }

    /// <summary>Keep the browser visible for manual authentication and optionally wait for a post-login selector.</summary>
    public bool ManualLogin { get; set; }

    /// <summary>CSS selector that indicates a manual or enterprise login flow has completed.</summary>
    public string? LoginSuccessSelector { get; set; }

    /// <summary>Timeout in milliseconds used when waiting for <see cref="LoginSuccessSelector"/>.</summary>
    public int LoginTimeout { get; set; } = 120000;

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting so their fields can be inspected safely.</summary>
    public bool PreventSsoAutoSubmit { get; set; }

    /// <summary>Run the browser without a visible window.</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Delay Playwright actions by this number of milliseconds.</summary>
    public int SlowMo { get; set; }

    /// <summary>Optional output file path for video recording.</summary>
    public string? VideoPath { get; set; }

    /// <summary>Recorded video width.</summary>
    public int VideoWidth { get; set; } = 800;

    /// <summary>Recorded video height.</summary>
    public int VideoHeight { get; set; } = 600;

    /// <summary>Playwright storage-state JSON file to import into a non-persistent browser context.</summary>
    public string? StorageStatePath { get; set; }

    /// <summary>Persistent browser user-data directory. When set, HtmlTinkerX launches a persistent context.</summary>
    public string? UserDataDirectory { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    public string? BrowserChannel { get; set; }

    /// <summary>Path to a browser executable. Use only when the bundled or channel browser is not desired.</summary>
    public string? BrowserExecutablePath { get; set; }

    /// <summary>Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.</summary>
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Additional browser command-line arguments.</summary>
    public IList<string> BrowserArguments { get; } = new List<string>();

    /// <summary>Enable Chromium sandboxing when supported by the selected browser.</summary>
    public bool? ChromiumSandbox { get; set; }

    /// <summary>Custom user agent string for the browser context.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Locale used by the browser context, for example en-US or pl-PL.</summary>
    public string? Locale { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    public int? ViewportHeight { get; set; }

    /// <summary>Screen width in pixels for persistent contexts.</summary>
    public int? ScreenWidth { get; set; }

    /// <summary>Screen height in pixels for persistent contexts.</summary>
    public int? ScreenHeight { get; set; }

    /// <summary>Device scale factor.</summary>
    public float? DeviceScaleFactor { get; set; }

    /// <summary>Whether the context should behave as a mobile browser where supported.</summary>
    public bool? IsMobile { get; set; }

    /// <summary>Whether touch input should be exposed to the page where supported.</summary>
    public bool? HasTouch { get; set; }

    /// <summary>Proxy server URL.</summary>
    public string? Proxy { get; set; }

    /// <summary>Proxy username.</summary>
    public string? ProxyUsername { get; set; }

    /// <summary>Proxy password.</summary>
    public string? ProxyPassword { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier used by the browser JavaScript runtime.</summary>
    public string? Timezone { get; set; }

    /// <summary>Browser permissions granted to pages in the context.</summary>
    public IList<string> Permissions { get; } = new List<string>();

    /// <summary>JavaScript snippets evaluated before page scripts run.</summary>
    public IList<string> InitScripts { get; } = new List<string>();

    /// <summary>JavaScript files evaluated before page scripts run.</summary>
    public IList<string> InitScriptPaths { get; } = new List<string>();

    /// <summary>Browser resource types to block before first navigation.</summary>
    public IList<HtmlNetworkResourceType> BlockResourceTypes { get; } = new List<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to block before first navigation.</summary>
    public IList<string> BlockResourcePatterns { get; } = new List<string>();

    /// <summary>Initial navigation readiness state.</summary>
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Navigation and selector timeout in milliseconds.</summary>
    public int Timeout { get; set; } = 10000;

    /// <summary>Creates a launch options object equivalent to the historical HtmlBrowser overload parameters.</summary>
    public static HtmlBrowserLaunchOptions FromLegacyParameters(
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? videoPath = null,
        int videoWidth = 800,
        int videoHeight = 600,
        string? storageStatePath = null,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null,
        IEnumerable<HtmlNetworkResourceType>? blockResourceTypes = null,
        IEnumerable<string>? blockResourcePatterns = null,
        HtmlBrowserLoadState loadState = HtmlBrowserLoadState.NetworkIdle,
        int timeout = 10000) {
        HtmlBrowserLaunchOptions options = new() {
            Browser = browser,
            Clean = clean,
            Username = username,
            Password = password,
            FormLogin = formLogin,
            Headless = headless,
            SlowMo = slowMo,
            VideoPath = videoPath,
            VideoWidth = videoWidth,
            VideoHeight = videoHeight,
            StorageStatePath = storageStatePath,
            UserAgent = userAgent,
            ViewportWidth = viewportWidth,
            ViewportHeight = viewportHeight,
            DeviceScaleFactor = deviceScaleFactor,
            Proxy = proxy,
            ProxyUsername = proxyUsername,
            ProxyPassword = proxyPassword,
            GeoLatitude = geoLatitude,
            GeoLongitude = geoLongitude,
            Timezone = timezone,
            LoadState = loadState,
            Timeout = timeout
        };

        AddRange(options.BlockResourceTypes, blockResourceTypes);
        AddRange(options.BlockResourcePatterns, blockResourcePatterns);
        return options;
    }

    /// <summary>Applies browser profile values after scenario defaults so explicit profile fields can refine the scenario.</summary>
    public void ApplyProfile(HtmlBrowserProfile? profile) {
        if (profile == null) {
            return;
        }

        if (profile.Scenario.HasValue) {
            ApplyScenario(profile.Scenario.Value);
        }

        if (profile.Browser.HasValue) {
            Browser = profile.Browser.Value;
        }

        ApplyIfSet(profile.UserDataDirectory, value => UserDataDirectory = value);
        ApplyIfSet(profile.BrowserChannel, value => BrowserChannel = value);
        ApplyIfSet(profile.BrowserExecutablePath, value => BrowserExecutablePath = value);
        ApplyIfSet(profile.CdpEndpointUrl, value => CdpEndpointUrl = value);
        ApplyIfSet(profile.UserAgent, value => UserAgent = value);
        ApplyIfSet(profile.LoadState, value => LoadState = value);
        ApplyIfSet(profile.Timeout, value => Timeout = value);
        ApplyIfSet(profile.Locale, value => Locale = value);
        ApplyIfSet(profile.Timezone, value => Timezone = value);
        ApplyIfSet(profile.ViewportWidth, value => ViewportWidth = value);
        ApplyIfSet(profile.ViewportHeight, value => ViewportHeight = value);
        ApplyIfSet(profile.ScreenWidth, value => ScreenWidth = value);
        ApplyIfSet(profile.ScreenHeight, value => ScreenHeight = value);
        ApplyIfSet(profile.DeviceScaleFactor, value => DeviceScaleFactor = value);
        ApplyIfSet(profile.IsMobile, value => IsMobile = value);
        ApplyIfSet(profile.HasTouch, value => HasTouch = value);
        ApplyIfSet(profile.Proxy, value => Proxy = value);
        ApplyIfSet(profile.ProxyUsername, value => ProxyUsername = value);
        ApplyIfSet(profile.ProxyPassword, value => ProxyPassword = value);
        ApplyIfSet(profile.GeoLatitude, value => GeoLatitude = value);
        ApplyIfSet(profile.GeoLongitude, value => GeoLongitude = value);
        ApplyIfSet(profile.ChromiumSandbox, value => ChromiumSandbox = value);
        ApplyIfSet(profile.PreventSsoAutoSubmit, value => PreventSsoAutoSubmit = value);

        AddMissing(BrowserArguments, profile.BrowserArguments);
        AddMissing(Permissions, profile.Permissions);
        ValidateResourceTypes(profile.BlockResourceTypes);
        AddMissing(BlockResourceTypes, profile.BlockResourceTypes);
        AddMissing(BlockResourcePatterns, profile.BlockResourcePatterns);
        AddMissing(InitScripts, profile.InitScripts);
        AddMissing(InitScriptPaths, profile.InitScriptPaths);
    }

    /// <summary>Applies scenario defaults before profiles or explicit caller options refine the launch configuration.</summary>
    public void ApplyScenario(HtmlBrowserScenario scenario) {
        if (scenario == HtmlBrowserScenario.Custom) {
            Scenario = scenario;
            return;
        }

        Scenario = scenario;

        switch (scenario) {
            case HtmlBrowserScenario.AuditProof:
                ApplyEvidenceDefaults(timeout: 30000);
                break;
            case HtmlBrowserScenario.MailboxProof:
                ApplyEvidenceDefaults(timeout: 30000);
                break;
            case HtmlBrowserScenario.LoginProtected:
                ApplyDomReadyDefaults(timeout: 60000);
                break;
            case HtmlBrowserScenario.SinglePageApp:
                ApplyDomReadyDefaults(timeout: 30000);
                break;
            case HtmlBrowserScenario.LowBandwidth:
                ApplyDomReadyDefaults(timeout: 30000);
                AddMissing(BlockResourceTypes, new[] {
                    HtmlNetworkResourceType.Image,
                    HtmlNetworkResourceType.Media,
                    HtmlNetworkResourceType.Font
                });
                break;
            case HtmlBrowserScenario.NetworkCapture:
                ApplyDomReadyDefaults(timeout: 30000);
                break;
            case HtmlBrowserScenario.DownloadEvidence:
                ApplyEvidenceDefaults(timeout: 30000);
                break;
        }
    }

    private void ApplyEvidenceDefaults(int timeout) {
        ViewportWidth ??= 1366;
        ViewportHeight ??= 900;
        ScreenWidth ??= ViewportWidth;
        ScreenHeight ??= ViewportHeight;
        ApplyDomReadyDefaults(timeout);
    }

    private void ApplyDomReadyDefaults(int timeout) {
        if (LoadState == HtmlBrowserLoadState.NetworkIdle) {
            LoadState = HtmlBrowserLoadState.DomContentLoaded;
        }

        if (Timeout <= 10000) {
            Timeout = timeout;
        }
    }

    private static void AddRange<T>(IList<T> target, IEnumerable<T>? values) {
        if (values == null) {
            return;
        }

        foreach (T value in values) {
            target.Add(value);
        }
    }

    private static void AddMissing<T>(IList<T> target, IEnumerable<T>? values) {
        if (values == null) {
            return;
        }

        foreach (T value in values) {
            if (!target.Contains(value)) {
                target.Add(value);
            }
        }
    }

    private static void ValidateResourceTypes(IEnumerable<HtmlNetworkResourceType>? values) {
        if (values == null) {
            return;
        }

        foreach (HtmlNetworkResourceType value in values) {
            if (value == HtmlNetworkResourceType.Document) {
                throw new ArgumentException("BlockResourceType Document would abort page navigation. Block subresources such as Image, Media, Font, Stylesheet, Script, XHR, or Fetch instead.");
            }
        }
    }

    private static void ApplyIfSet<T>(T? value, Action<T> setter) where T : struct {
        if (value.HasValue) {
            setter(value.Value);
        }
    }

    private static void ApplyIfSet(string? value, Action<string> setter) {
        if (!string.IsNullOrWhiteSpace(value)) {
            setter(value!);
        }
    }
}
