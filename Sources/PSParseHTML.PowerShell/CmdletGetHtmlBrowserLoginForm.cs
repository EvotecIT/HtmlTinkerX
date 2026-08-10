using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that detects a login form and returns the selectors needed for form authentication.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserLoginForm", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlFormLogin))]
[Alias("Get-HtmlLoginForm")]
public sealed class CmdletGetHtmlBrowserLoginForm : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL to inspect.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Browser engine used when loading <see cref="Url"/> or <see cref="Path"/>.</summary>
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

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Proxy server address.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Proxy credentials.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Delay in milliseconds between actions.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Timeout in milliseconds for browser operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Initial browser navigation readiness state.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlFormLogin? result;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                HtmlBrowserLaunchOptions urlOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(Url!, urlOptions, token).ConfigureAwait(false)) {
                    result = await HtmlBrowser.DetectLoginFormAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            case ParameterSetFile:
                string fileUrl = HtmlBrowser.CreateLocalFileUri(Path!).AbsoluteUri;
                HtmlBrowserLaunchOptions fileOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(fileUrl, fileOptions, token).ConfigureAwait(false)) {
                    result = await HtmlBrowser.DetectLoginFormAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            default:
                HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                result = await HtmlBrowser.DetectLoginFormAsync(session.Page, token).ConfigureAwait(false);
                break;
        }

        if (result != null) {
            WriteObject(result);
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
            Timeout = Timeout,
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
