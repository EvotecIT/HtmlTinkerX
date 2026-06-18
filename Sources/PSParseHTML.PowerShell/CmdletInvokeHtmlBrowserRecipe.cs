using HtmlTinkerX;
using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Executes a replayable browser automation recipe.
/// </summary>
/// <example>
///   <summary>Run a browser recipe from disk</summary>
///   <code>Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json</code>
/// </example>
/// <example>
///   <summary>Run a browser recipe against an existing session</summary>
///   <code>$recipe | Invoke-HtmlBrowserRecipe -Session $session</code>
/// </example>
/// <example>
///   <summary>Run a browser recipe with runtime variables from JSON</summary>
///   <code>Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -VariablePath .\browser.recipe.variables.json</code>
/// </example>
/// <example>
///   <summary>Run replay even when you want to bypass the default preflight check</summary>
///   <code>Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -SkipPreflight</code>
/// </example>
/// <example>
///   <summary>Block replay on preflight warnings for scheduled jobs</summary>
///   <code>Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -StrictPreflight</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserRecipe", DefaultParameterSetName = ParameterSetRecipe)]
[OutputType(typeof(HtmlBrowserRecipeRunResult))]
public sealed class CmdletInvokeHtmlBrowserRecipe : AsyncPSCmdlet {
    private const string ParameterSetRecipe = "Recipe";
    private const string ParameterSetPath = "Path";

    /// <summary>Recipe object to execute.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetRecipe, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserRecipe? Recipe { get; set; }

    /// <summary>Recipe JSON path.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Existing browser session. When omitted, the recipe must provide StartUrl.</summary>
    [Parameter]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Optional browser profile JSON file used as launch defaults when the recipe creates a session.</summary>
    [Parameter]
    public string? ProfilePath { get; set; }

    /// <summary>Persistent browser user-data directory for cookies, storage, cache, and permissions.</summary>
    [Parameter]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file for cookies and local storage.</summary>
    [Parameter]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser engine to use when the recipe creates a session.</summary>
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

    /// <summary>Additional browser command-line arguments.</summary>
    [Parameter]
    public string[] BrowserArgument { get; set; } = Array.Empty<string>();

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

    /// <summary>Navigation timeout in milliseconds used when the recipe creates a session.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int NavigationTimeout { get; set; } = 10000;

    /// <summary>Credentials used when accessing authenticated pages.</summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Login page URL used for form-based authentication before navigating to the recipe StartUrl.</summary>
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

    /// <summary>Open a visible browser for manual MFA/SSO login before replaying recipe steps.</summary>
    [Parameter]
    public SwitchParameter ManualLogin { get; set; }

    /// <summary>CSS selector that indicates manual login completed successfully.</summary>
    [Parameter]
    public string? LoginSuccessSelector { get; set; }

    /// <summary>Timeout in milliseconds used when waiting for <see cref="LoginSuccessSelector"/>.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int LoginTimeout { get; set; } = 120000;

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting so hidden assertion fields can be inspected.</summary>
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
    public string[] Permission { get; set; } = Array.Empty<string>();

    /// <summary>JavaScript snippets evaluated before page scripts run.</summary>
    [Parameter]
    public string[] InitScript { get; set; } = Array.Empty<string>();

    /// <summary>JavaScript files evaluated before page scripts run.</summary>
    [Parameter]
    public string[] InitScriptPath { get; set; } = Array.Empty<string>();

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter]
    public string[] BlockResourcePattern { get; set; } = Array.Empty<string>();

    /// <summary>Runtime variables used to replace redacted or parameterized recipe step values.</summary>
    [Parameter]
    [Alias("RecipeVariable")]
    public IDictionary? Variable { get; set; }

    /// <summary>JSON file containing runtime variables. Placeholder values such as &lt;secret&gt; are ignored so templates cannot replay as literal secrets.</summary>
    [Parameter]
    public string? VariablePath { get; set; }

    /// <summary>Skip default preflight validation before launching or using a browser session.</summary>
    [Parameter]
    public SwitchParameter SkipPreflight { get; set; }

    /// <summary>Treat preflight warnings as blocking issues before launching or using a browser session.</summary>
    [Parameter]
    public SwitchParameter StrictPreflight { get; set; }

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context when a recipe step fails.</summary>
    [Parameter]
    public SwitchParameter OnFailureEvidence { get; set; }

    /// <summary>Root folder where recipe failure evidence is written when <see cref="OnFailureEvidence"/> is used.</summary>
    [Parameter]
    public string? FailureEvidenceFolder { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserRecipe recipe = await GetRecipeAsync().ConfigureAwait(false);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserRecipeRunOptions options = await CreateRunOptionsAsync(recipe, linkedCts.Token, createLaunchOptions: false).ConfigureAwait(false);
        HtmlBrowserRecipeValidationResult? validation = null;
        if (!SkipPreflight.IsPresent) {
            validation = HtmlBrowser.ValidateRecipe(recipe, options.Variables.Keys, Session != null, StrictPreflight.IsPresent);
            if (validation.HasBlockingIssues(StrictPreflight.IsPresent)) {
                WriteObject(HtmlBrowser.CreateRecipePreflightFailureResult(recipe, validation, StrictPreflight.IsPresent));
                return;
            }
        }

        if (Session == null) {
            options.LaunchOptions = await CreateLaunchOptionsAsync(recipe, linkedCts.Token).ConfigureAwait(false);
        }

        HtmlBrowserRecipeRunResult result = await HtmlBrowser.ExecuteRecipeAsync(recipe, Session, options, linkedCts.Token).ConfigureAwait(false);
        result.Validation = validation;
        result.StrictPreflight = StrictPreflight.IsPresent;
        WriteObject(result);
    }

    private async Task<HtmlBrowserRecipeRunOptions> CreateRunOptionsAsync(HtmlBrowserRecipe recipe, CancellationToken cancellationToken, bool createLaunchOptions = true) {
        HtmlBrowserRecipeRunOptions options = new() {
            OnFailureEvidence = OnFailureEvidence.IsPresent,
            FailureEvidenceFolder = FailureEvidenceFolder
        };

        if (createLaunchOptions && Session == null) {
            options.LaunchOptions = await CreateLaunchOptionsAsync(recipe, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(VariablePath)) {
            System.Collections.Generic.Dictionary<string, string> fileVariables = await HtmlBrowser.LoadRecipeVariablesAsync(VariablePath!, cancellationToken).ConfigureAwait(false);
            foreach (System.Collections.Generic.KeyValuePair<string, string> variable in fileVariables) {
                options.Variables[variable.Key] = variable.Value;
            }
        }

        if (Variable == null) {
            return options;
        }

        foreach (DictionaryEntry entry in Variable) {
            if (entry.Key == null) {
                continue;
            }

            string name = entry.Key.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) {
                continue;
            }

            options.Variables[name] = ConvertVariableValue(entry.Value);
        }

        return options;
    }

    private async Task<HtmlBrowserLaunchOptions> CreateLaunchOptionsAsync(HtmlBrowserRecipe recipe, CancellationToken cancellationToken) {
        HtmlBrowserLaunchOptions launchOptions = await HtmlBrowserLaunchOptionFactory.CreateAsync(new HtmlBrowserLaunchOptionRequest {
            BoundParameters = MyInvocation.BoundParameters,
            BaseOptions = HtmlBrowser.CreateRecipeLaunchOptions(recipe),
            ProfilePath = ProfilePath,
            Scenario = Scenario,
            Browser = Browser,
            Clean = Clean,
            Visible = Visible,
            SlowMo = SlowMo,
            Timeout = NavigationTimeout,
            TimeoutParameterName = nameof(NavigationTimeout),
            UserDataDirectory = UserDataDirectory,
            StatePath = StatePath,
            BrowserChannel = BrowserChannel,
            BrowserExecutablePath = BrowserExecutablePath,
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
            BlockResourceType = BlockResourceType,
            BlockResourcePattern = BlockResourcePattern
        }, cancellationToken).ConfigureAwait(false);

        ApplyRecipeSessionParameters(launchOptions);
        return launchOptions;
    }

    private void ApplyRecipeSessionParameters(HtmlBrowserLaunchOptions options) {
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

    private void SetIfBound<T>(string parameterName, Action<T?> setter, T? value) {
        if (MyInvocation.BoundParameters.ContainsKey(parameterName)) {
            setter(value);
        }
    }

    private static string ConvertVariableValue(object? value) {
        if (value == null) {
            return string.Empty;
        }

        if (value is SecureString secureString) {
            return ConvertSecureString(secureString);
        }

        if (value is PSCredential credential) {
            return credential.GetNetworkCredential().Password;
        }

        return value.ToString() ?? string.Empty;
    }

    private static string ConvertSecureString(SecureString secureString) {
        IntPtr pointer = IntPtr.Zero;
        try {
            pointer = Marshal.SecureStringToBSTR(secureString);
            return Marshal.PtrToStringBSTR(pointer) ?? string.Empty;
        } finally {
            if (pointer != IntPtr.Zero) {
                Marshal.ZeroFreeBSTR(pointer);
            }
        }
    }

    private async Task<HtmlBrowserRecipe> GetRecipeAsync() {
        if (ParameterSetName == ParameterSetRecipe) {
            return Recipe!;
        }

        string fullPath = Path!.ToFullPath();
        string json = await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        return HtmlBrowser.DeserializeRecipe(json);
    }
}
