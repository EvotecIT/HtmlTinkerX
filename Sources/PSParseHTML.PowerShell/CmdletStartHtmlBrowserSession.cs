using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Starts a browser session for rendered web automation, extraction, and evidence workflows.
/// </summary>
/// <example>
///   <summary>Start a visible browser session with a persistent work profile</summary>
///   <code>Start-HtmlBrowserSession -Url https://example.org -UserDataDirectory .\.profiles\work -BrowserChannel chrome -Visible</code>
/// </example>
[Cmdlet(VerbsLifecycle.Start, "HtmlBrowserSession", DefaultParameterSetName = ParameterSetUrl)]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Start-HtmlSession", "Open-HtmlSession")]
public sealed class CmdletStartHtmlBrowserSession : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>URL to navigate to.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Optional browser profile JSON file used as launch defaults.</summary>
    [Parameter]
    public string? ProfilePath { get; set; }

    /// <summary>Persistent browser user-data directory for cookies, storage, cache, and permissions.</summary>
    [Parameter]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file for cookies and local storage.</summary>
    [Parameter]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser engine to use.</summary>
    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Intent-focused browser automation defaults to apply before explicit parameter values.</summary>
    [Parameter]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter]
    public string? BrowserChannel { get; set; }

    /// <summary>Path to a browser executable.</summary>
    [Parameter]
    public string? BrowserExecutablePath { get; set; }

    /// <summary>Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.</summary>
    [Parameter]
    [Alias("CdpEndpoint", "RemoteDebuggingUrl")]
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Additional browser command-line arguments.</summary>
    [Parameter]
    public string[] BrowserArgument { get; set; } = System.Array.Empty<string>();

    /// <summary>Enable Chromium sandboxing when supported.</summary>
    [Parameter]
    public SwitchParameter ChromiumSandbox { get; set; }

    /// <summary>Force browser runtime reinstall before launch.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Delay Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; }

    /// <summary>Timeout in milliseconds for navigation and browser operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Credentials used when accessing authenticated pages.</summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Login page URL used for form-based authentication before navigating to the requested URL.</summary>
    [Parameter]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field used with <see cref="LoginUrl"/>.</summary>
    [Parameter]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field used with <see cref="LoginUrl"/>.</summary>
    [Parameter]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit button used with <see cref="LoginUrl"/>.</summary>
    [Parameter]
    public string? SubmitSelector { get; set; }

    /// <summary>Open a visible browser for manual MFA/SSO login and optionally wait for a post-login selector.</summary>
    [Parameter]
    public SwitchParameter ManualLogin { get; set; }

    /// <summary>CSS selector that indicates manual login completed successfully.</summary>
    [Parameter]
    public string? LoginSuccessSelector { get; set; }

    /// <summary>Timeout in milliseconds used when waiting for <see cref="LoginSuccessSelector"/>.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int LoginTimeout { get; set; } = 120000;

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting so their hidden assertion fields can be inspected.</summary>
    [Parameter]
    public SwitchParameter PreventSsoAutoSubmit { get; set; }

    /// <summary>Username for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Username { get; set; }

    /// <summary>Password for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Password { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Proxy credentials.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User agent string used by the browser context.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Locale used by the browser context, such as en-US or pl-PL.</summary>
    [Parameter]
    public string? Locale { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportHeight { get; set; }

    /// <summary>Screen width in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ScreenWidth { get; set; }

    /// <summary>Screen height in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ScreenHeight { get; set; }

    /// <summary>Scaling factor for high DPI devices.</summary>
    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    /// <summary>Expose mobile browser behavior where supported.</summary>
    [Parameter]
    public SwitchParameter Mobile { get; set; }

    /// <summary>Expose touch input where supported.</summary>
    [Parameter]
    public SwitchParameter Touch { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier used by the browser JavaScript runtime.</summary>
    [Parameter]
    public string? Timezone { get; set; }

    /// <summary>Browser permissions granted to pages in the context.</summary>
    [Parameter]
    public string[] Permission { get; set; } = System.Array.Empty<string>();

    /// <summary>JavaScript snippets evaluated before page scripts run.</summary>
    [Parameter]
    public string[] InitScript { get; set; } = System.Array.Empty<string>();

    /// <summary>JavaScript files evaluated before page scripts run.</summary>
    [Parameter]
    public string[] InitScriptPath { get; set; } = System.Array.Empty<string>();

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Initial browser navigation readiness state.</summary>
    [Parameter]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Do not store this session as the default session.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        HtmlBrowserLaunchOptions options = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
        ApplySessionParameters(options);
        ValidateProxy(options.Proxy, ProxyCredential);

        string target = ParameterSetName == ParameterSetFile
            ? new System.Uri(Path!.ToFullPath()).AbsoluteUri
            : Url!;

        HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(target, options, token).ConfigureAwait(false);
        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", session);
        }

        WriteObject(session);
    }

    private async Task<HtmlBrowserLaunchOptions> CreateLaunchOptionsAsync(CancellationToken cancellationToken) {
        return await HtmlBrowserLaunchOptionFactory.CreateAsync(new HtmlBrowserLaunchOptionRequest {
            BoundParameters = MyInvocation.BoundParameters,
            ProfilePath = ProfilePath,
            Scenario = Scenario,
            Browser = Browser,
            Clean = Clean,
            Visible = Visible,
            SlowMo = SlowMo,
            Timeout = Timeout,
            UserDataDirectory = UserDataDirectory,
            StatePath = StatePath,
            BrowserChannel = BrowserChannel,
            BrowserExecutablePath = BrowserExecutablePath,
            CdpEndpointUrl = CdpEndpointUrl,
            BrowserArgument = BrowserArgument,
            ChromiumSandbox = ChromiumSandbox,
            Proxy = Proxy,
            ProxyCredential = ProxyCredential,
            UserAgent = UserAgent,
            Locale = Locale,
            ViewportWidth = ViewportWidth,
            ViewportHeight = ViewportHeight,
            ScreenWidth = ScreenWidth,
            ScreenHeight = ScreenHeight,
            DeviceScaleFactor = DeviceScaleFactor,
            Mobile = Mobile,
            Touch = Touch,
            GeoLatitude = GeoLatitude,
            GeoLongitude = GeoLongitude,
            Timezone = Timezone,
            Permission = Permission,
            InitScript = InitScript,
            InitScriptPath = InitScriptPath,
            LoadState = LoadState,
            BlockResourceType = BlockResourceType,
            BlockResourcePattern = BlockResourcePattern
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ApplySessionParameters(HtmlBrowserLaunchOptions options) {
        options.Username = Credential?.UserName ?? Username ?? options.Username;
        options.Password = Credential?.GetNetworkCredential().Password ?? Password ?? options.Password;
        options.FormLogin = CreateFormLogin() ?? options.FormLogin;
        options.ManualLogin = ManualLogin.IsPresent || MyInvocation.BoundParameters.ContainsKey(nameof(LoginSuccessSelector)) || options.ManualLogin;
        if (options.ManualLogin) {
            options.Headless = false;
        }

        if (PreventSsoAutoSubmit.IsPresent) {
            options.PreventSsoAutoSubmit = true;
        }

        SetIfBound(nameof(LoginSuccessSelector), value => options.LoginSuccessSelector = value, LoginSuccessSelector);
        options.LoginTimeout = MyInvocation.BoundParameters.ContainsKey(nameof(LoginTimeout)) ? LoginTimeout : options.LoginTimeout;
    }

    private HtmlFormLogin? CreateFormLogin() {
        bool hasAnyLoginParameter = !string.IsNullOrWhiteSpace(LoginUrl)
            || !string.IsNullOrWhiteSpace(UsernameSelector)
            || !string.IsNullOrWhiteSpace(PasswordSelector)
            || !string.IsNullOrWhiteSpace(SubmitSelector);
        if (!hasAnyLoginParameter) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(LoginUrl)
            || string.IsNullOrWhiteSpace(UsernameSelector)
            || string.IsNullOrWhiteSpace(PasswordSelector)
            || string.IsNullOrWhiteSpace(SubmitSelector)) {
            throw new PSArgumentException("LoginUrl, UsernameSelector, PasswordSelector, and SubmitSelector must be specified together.");
        }

        return new HtmlFormLogin {
            LoginUrl = LoginUrl!,
            UsernameSelector = UsernameSelector!,
            PasswordSelector = PasswordSelector!,
            SubmitSelector = SubmitSelector!
        };
    }

    private void SetIfBound<T>(string parameterName, System.Action<T?> setter, T? value) {
        if (MyInvocation.BoundParameters.ContainsKey(parameterName)) {
            setter(value);
        }
    }

}
