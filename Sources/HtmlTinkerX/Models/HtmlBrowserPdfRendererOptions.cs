namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Immutable lifecycle and context options for <see cref="HtmlBrowserPdfRenderer"/>.</summary>
public sealed class HtmlBrowserPdfRendererOptions {
    /// <summary>Initializes renderer options.</summary>
    public HtmlBrowserPdfRendererOptions(
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        int minimumBrowserInstances = 0,
        int maximumBrowserInstances = 2,
        int maximumQueuedCaptures = 32,
        int maximumRendersPerBrowser = 250,
        TimeSpan? maximumBrowserAge = null,
        bool headless = true,
        bool ignoreHttpsErrors = false,
        string? browserChannel = null,
        string? browserExecutablePath = null,
        IEnumerable<string>? browserArguments = null,
        bool? chromiumSandbox = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        string? storageStatePath = null,
        string? userAgent = null,
        string? locale = null,
        string? timezone = null,
        int? viewportWidth = 1440,
        int? viewportHeight = 900,
        float? deviceScaleFactor = null,
        bool? isMobile = null,
        bool? hasTouch = null,
        HtmlBrowserNetworkPolicy? networkPolicy = null,
        TimeSpan? setupTimeout = null) {
        if (browser != HtmlBrowserEngine.Chromium) {
            throw new NotSupportedException("Browser PDF capture is supported only by Chromium. Firefox and WebKit requests must use a non-PDF browser capability.");
        }
        if (minimumBrowserInstances < 0) throw new ArgumentOutOfRangeException(nameof(minimumBrowserInstances));
        if (maximumBrowserInstances <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBrowserInstances));
        if (minimumBrowserInstances > maximumBrowserInstances) throw new ArgumentOutOfRangeException(nameof(minimumBrowserInstances));
        if (maximumQueuedCaptures < 0) throw new ArgumentOutOfRangeException(nameof(maximumQueuedCaptures));
        if (maximumRendersPerBrowser <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRendersPerBrowser));
        if (maximumBrowserAge.HasValue && maximumBrowserAge.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumBrowserAge));
        if (setupTimeout.HasValue && setupTimeout.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(setupTimeout));
        if (viewportWidth.HasValue != viewportHeight.HasValue) throw new ArgumentException("Viewport width and height must be provided together.");
        if (viewportWidth <= 0 || viewportHeight <= 0) throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (deviceScaleFactor.HasValue
            && (deviceScaleFactor.Value <= 0F
                || float.IsNaN(deviceScaleFactor.Value)
                || float.IsInfinity(deviceScaleFactor.Value))) {
            throw new ArgumentOutOfRangeException(nameof(deviceScaleFactor));
        }
        if (string.IsNullOrWhiteSpace(proxy)
            && (!string.IsNullOrWhiteSpace(proxyUsername) || !string.IsNullOrWhiteSpace(proxyPassword))) {
            throw new ArgumentException("Proxy credentials require a caller-supplied proxy server.", nameof(proxy));
        }

        Browser = browser;
        MinimumBrowserInstances = minimumBrowserInstances;
        MaximumBrowserInstances = maximumBrowserInstances;
        MaximumQueuedCaptures = maximumQueuedCaptures;
        MaximumRendersPerBrowser = maximumRendersPerBrowser;
        MaximumBrowserAge = maximumBrowserAge ?? TimeSpan.FromMinutes(30);
        SetupTimeout = setupTimeout ?? TimeSpan.FromSeconds(30);
        Headless = headless;
        IgnoreHttpsErrors = ignoreHttpsErrors;
        BrowserChannel = NormalizeOptional(browserChannel);
        BrowserExecutablePath = NormalizeOptional(browserExecutablePath);
        BrowserArguments = Array.AsReadOnly((browserArguments ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
        ChromiumSandbox = chromiumSandbox;
        Proxy = NormalizeOptional(proxy);
        ProxyUsername = proxyUsername;
        ProxyPassword = proxyPassword;
        StorageStatePath = NormalizeOptional(storageStatePath);
        UserAgent = NormalizeOptional(userAgent);
        Locale = NormalizeOptional(locale);
        Timezone = NormalizeOptional(timezone);
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        DeviceScaleFactor = deviceScaleFactor;
        IsMobile = isMobile;
        HasTouch = hasTouch;
        NetworkPolicy = networkPolicy ?? HtmlBrowserNetworkPolicy.PublicNetworkOnly;
    }

    /// <summary>Gets the browser engine. This is always Chromium.</summary>
    public HtmlBrowserEngine Browser { get; }
    /// <summary>Gets the number of browsers created by <see cref="HtmlBrowserPdfRenderer.PreWarmAsync"/>.</summary>
    public int MinimumBrowserInstances { get; }
    /// <summary>Gets the maximum number of simultaneous browser leases.</summary>
    public int MaximumBrowserInstances { get; }
    /// <summary>Gets the maximum number of captures waiting behind active leases.</summary>
    public int MaximumQueuedCaptures { get; }
    /// <summary>Gets the number of renders after which a browser is recycled.</summary>
    public int MaximumRendersPerBrowser { get; }
    /// <summary>Gets the maximum browser lifetime.</summary>
    public TimeSpan MaximumBrowserAge { get; }
    /// <summary>Gets the renderer-owned deadline for browser provisioning, isolated context, and page setup before navigation.</summary>
    public TimeSpan SetupTimeout { get; }
    /// <summary>Gets whether browser processes are headless.</summary>
    public bool Headless { get; }
    /// <summary>Gets whether HTTPS certificate errors are ignored.</summary>
    public bool IgnoreHttpsErrors { get; }
    /// <summary>Gets the optional Chromium channel.</summary>
    public string? BrowserChannel { get; }
    /// <summary>Gets the optional Chromium executable path.</summary>
    public string? BrowserExecutablePath { get; }
    /// <summary>Gets additional Chromium command-line arguments.</summary>
    public IReadOnlyList<string> BrowserArguments { get; }
    /// <summary>Gets the Chromium sandbox override.</summary>
    public bool? ChromiumSandbox { get; }
    /// <summary>Gets the proxy server.</summary>
    public string? Proxy { get; }
    /// <summary>Gets the proxy username.</summary>
    public string? ProxyUsername { get; }
    /// <summary>Gets the proxy password.</summary>
    public string? ProxyPassword { get; }
    /// <summary>Gets the Playwright storage-state file loaded into each isolated context.</summary>
    public string? StorageStatePath { get; }
    /// <summary>Gets the context user agent.</summary>
    public string? UserAgent { get; }
    /// <summary>Gets the context locale.</summary>
    public string? Locale { get; }
    /// <summary>Gets the context timezone.</summary>
    public string? Timezone { get; }
    /// <summary>Gets the context viewport width.</summary>
    public int? ViewportWidth { get; }
    /// <summary>Gets the context viewport height.</summary>
    public int? ViewportHeight { get; }
    /// <summary>Gets the browser-context device pixel ratio.</summary>
    public float? DeviceScaleFactor { get; }
    /// <summary>Gets whether the browser context emulates mobile layout behavior.</summary>
    public bool? IsMobile { get; }
    /// <summary>Gets whether the browser context exposes touch input.</summary>
    public bool? HasTouch { get; }
    /// <summary>Gets the resource access policy enforced for every capture.</summary>
    public HtmlBrowserNetworkPolicy NetworkPolicy { get; }

    internal bool RequiresManagedPolicyProxy =>
        string.IsNullOrWhiteSpace(Proxy)
        && (!NetworkPolicy.AllowPrivateNetworks
            || NetworkPolicy.AllowedHosts.Count > 0
            || NetworkPolicy.DeniedHosts.Count > 0);

    internal bool ProxyOwnsNetworkResolution =>
        !string.IsNullOrWhiteSpace(Proxy)
        && NetworkPolicy.AllowPrivateNetworks
        && NetworkPolicy.AllowedHosts.Count == 0
        && NetworkPolicy.DeniedHosts.Count == 0;

    internal HtmlBrowserLaunchOptions CreateLaunchOptions() {
        HtmlBrowserLaunchOptions options = new() {
            Browser = Browser,
            Headless = Headless,
            IgnoreHTTPSErrors = IgnoreHttpsErrors,
            BrowserChannel = BrowserChannel,
            BrowserExecutablePath = BrowserExecutablePath,
            ChromiumSandbox = ChromiumSandbox,
            Proxy = Proxy,
            ProxyUsername = ProxyUsername,
            ProxyPassword = ProxyPassword
        };
        foreach (string argument in BrowserArguments) options.BrowserArguments.Add(argument);
        if (RequiresManagedPolicyProxy) {
            // Chromium implicitly bypasses proxies for loopback unless this subtraction rule
            // is present. The capture-scoped policy proxy must observe loopback WS/WSS too.
            options.BrowserArguments.Add("--proxy-bypass-list=<-loopback>");
            // WebRTC ICE/STUN and QUIC use UDP paths that HTTP/CONNECT proxies cannot inspect.
            options.BrowserArguments.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
            options.BrowserArguments.Add("--disable-quic");
        }
        if (IgnoreHttpsErrors) options.BrowserArguments.Add("--ignore-certificate-errors");
        return options;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
