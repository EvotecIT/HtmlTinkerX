using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Gets SAML, WS-Federation, OAuth, or OpenID Connect form and URL callback handoffs from the current browser page.
/// </summary>
/// <example>
///   <summary>Capture an Azure SSO/SAML handoff and prepare an Invoke-WebRequest replay</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://portal.contoso.example -Visible -ManualLogin -PreventSsoAutoSubmit
/// $handoff = Get-HtmlBrowserSsoHandoff -Session $session -Wait -Timeout 60000
/// $handoff | Select-Object Kind, Action, FormData, SuggestedCommand, Warnings
/// $analysis = Get-HtmlBrowserSsoHandoff -Session $session -Analyze
/// $analysis | Select-Object Kind, Action, FieldNames, SamlResponse, JsonWebTokens, Warnings
///
/// # Only reveal assertion values when you intentionally need to replay the handoff.
/// $handoff = Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues
/// $webSession = ConvertTo-HtmlWebRequestSession -Session $session
/// Invoke-WebRequest -Uri $handoff.Action -Method $handoff.Method -Body $handoff.FormData -WebSession $webSession
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserSsoHandoff")]
[OutputType(typeof(HtmlBrowserSsoHandoff), typeof(HtmlSsoHandoffAnalysis))]
[Alias("Get-HtmlSsoHandoff")]
public sealed class CmdletGetHtmlBrowserSsoHandoff : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Existing browser session. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL of the SSO-protected page or handoff page to inspect.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Path to a local HTML file containing an SSO handoff page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Browser engine to use when loading <see cref="Url"/> or <see cref="Path"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Optional browser profile JSON file used as launch defaults.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? ProfilePath { get; set; }

    /// <summary>Intent-focused browser automation defaults to apply before explicit parameter values.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Persistent browser user-data directory for cookies, storage, cache, and permissions.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file for cookies and local storage.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? BrowserChannel { get; set; }

    /// <summary>Reinstall browser runtimes when using <see cref="Url"/> or <see cref="Path"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Timeout in milliseconds for the initial browser navigation when using <see cref="Url"/> or <see cref="Path"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int NavigationTimeout { get; set; } = 10000;

    /// <summary>Initial browser navigation readiness state.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.DomContentLoaded;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Include sensitive assertion, token, and state values. By default those values are redacted.</summary>
    [Parameter]
    public SwitchParameter IncludeSensitiveValues { get; set; }

    /// <summary>Return safe SAML, OAuth, or OpenID Connect protocol analysis instead of raw handoff form data.</summary>
    [Parameter]
    public SwitchParameter Analyze { get; set; }

    /// <summary>Include decoded SAML XML in analysis output. Sensitive XML values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeXml { get; set; }

    /// <summary>Include decoded JWT header and payload JSON in analysis output. Sensitive payload values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeJson { get; set; }

    /// <summary>Return all forms, not only forms with recognizable SSO handoff fields. URL callbacks still require recognizable protocol fields.</summary>
    [Parameter]
    public SwitchParameter IncludeAllForms { get; set; }

    /// <summary>Maximum field value length to return. Zero disables truncation.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxValueLength { get; set; } = 131072;

    /// <summary>Wait until at least one matching SSO handoff form or URL callback is observed.</summary>
    [Parameter]
    public SwitchParameter Wait { get; set; }

    /// <summary>Maximum time in milliseconds to wait when <see cref="Wait"/> is used. Zero waits indefinitely.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 30000;

    /// <summary>Polling interval in milliseconds while waiting for a handoff form.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int PollMilliseconds { get; set; } = 250;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSsoHandoffOptions options = new() {
            IncludeSensitiveValues = IncludeSensitiveValues.IsPresent || Analyze.IsPresent,
            IncludeAllForms = IncludeAllForms.IsPresent,
            MaxValueLength = MaxValueLength,
            Wait = Wait.IsPresent,
            Timeout = Timeout,
            PollMilliseconds = PollMilliseconds
        };

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        IReadOnlyList<HtmlBrowserSsoHandoff> handoffs;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                HtmlBrowserLaunchOptions urlOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
                ValidateProxy(urlOptions.Proxy, ProxyCredential);
                urlOptions.PreventSsoAutoSubmit = true;
                await using (HtmlBrowserSession urlSession = await HtmlBrowser.OpenSessionAsync(Url!, urlOptions, token).ConfigureAwait(false)) {
                    handoffs = await HtmlBrowser.GetSsoHandoffsAsync(urlSession, options, token).ConfigureAwait(false);
                }
                break;
            case ParameterSetFile:
                string fileUrl = HtmlBrowser.CreateLocalFileUri(Path!).AbsoluteUri;
                HtmlBrowserLaunchOptions fileOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
                ValidateProxy(fileOptions.Proxy, ProxyCredential);
                fileOptions.PreventSsoAutoSubmit = true;
                await using (HtmlBrowserSession fileSession = await HtmlBrowser.OpenSessionAsync(fileUrl, fileOptions, token).ConfigureAwait(false)) {
                    handoffs = await HtmlBrowser.GetSsoHandoffsAsync(fileSession, options, token).ConfigureAwait(false);
                }
                break;
            default:
                HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                handoffs = await HtmlBrowser.GetSsoHandoffsAsync(session, options, token).ConfigureAwait(false);
                break;
        }

        if (Analyze.IsPresent) {
            foreach (HtmlBrowserSsoHandoff handoff in handoffs) {
                WriteObject(HtmlSsoHandoffAnalyzer.Analyze(
                    handoff,
                    IncludeSensitiveValues.IsPresent,
                    IncludeXml.IsPresent,
                    IncludeJson.IsPresent));
            }
        } else {
            WriteObject(handoffs, true);
        }
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
            Timeout = NavigationTimeout,
            TimeoutParameterName = nameof(NavigationTimeout),
            UserDataDirectory = UserDataDirectory,
            StatePath = StatePath,
            BrowserChannel = BrowserChannel,
            Proxy = Proxy,
            ProxyCredential = ProxyCredential,
            LoadState = LoadState,
            BlockResourceType = BlockResourceType,
            BlockResourcePattern = BlockResourcePattern
        }, cancellationToken).ConfigureAwait(false);
    }
}
