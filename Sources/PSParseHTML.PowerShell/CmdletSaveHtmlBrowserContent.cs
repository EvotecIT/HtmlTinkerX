using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves rendered HTML or text content from an active browser session.
/// </summary>
/// <example>
///   <summary>Save rendered main content to disk</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/app
/// Save-HtmlBrowserContent -Session $session -Selector main -OutFile .\rendered-main.html
///   </code>
/// </example>
[Cmdlet(VerbsData.Save, "HtmlBrowserContent", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(string))]
[Alias("Save-HtmlContent")]
public sealed class CmdletSaveHtmlBrowserContent : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true, ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL to open for one-shot rendered content saving.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Local HTML file to open for one-shot rendered content saving.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Output file path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Optional selector to save.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Save inner HTML instead of outer HTML.</summary>
    [Parameter]
    public SwitchParameter InnerHtml { get; set; }

    /// <summary>Save text instead of HTML.</summary>
    [Parameter]
    public SwitchParameter AsText { get; set; }

    /// <summary>Timeout in milliseconds while waiting for the selector.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Optional browser profile JSON file used as launch defaults for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? ProfilePath { get; set; }

    /// <summary>Intent-focused browser automation defaults used for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Browser engine to use for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Persistent browser user-data directory used for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file used for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? BrowserChannel { get; set; }

    /// <summary>Show the browser instead of running headless for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Proxy server address used when launching the browser for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; }

    /// <summary>Initial browser navigation readiness state for URL or file saves.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Timeout in milliseconds for one-shot navigation and browser operations.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int NavigationTimeout { get; set; } = 10000;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Write the saved path to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        if (InnerHtml.IsPresent && AsText.IsPresent) {
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException("Specify only one of -InnerHtml or -AsText."),
                "InvalidParameter",
                ErrorCategory.InvalidArgument,
                Selector));
            return;
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        string fullPath = OutFile.ToFullPath();

        if (ParameterSetName == ParameterSetSession) {
            HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                ?? throw new PSInvalidOperationException("No session provided and no default session found.");
            await HtmlBrowser.SaveContentAsync(session, fullPath, Selector, InnerHtml.IsPresent, AsText.IsPresent, Timeout, token).ConfigureAwait(false);
        } else {
            string target = ParameterSetName == ParameterSetFile
                ? new System.Uri(Path!.ToFullPath()).AbsoluteUri
                : Url!;
            HtmlBrowserLaunchOptions launchOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
            await HtmlBrowser.SaveContentAsync(target, fullPath, launchOptions, Selector, InnerHtml.IsPresent, AsText.IsPresent, Timeout, token).ConfigureAwait(false);
        }

        if (PassThru.IsPresent) {
            WriteObject(fullPath);
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
            LoadState = LoadState,
            UserDataDirectory = UserDataDirectory,
            StatePath = StatePath,
            BrowserChannel = BrowserChannel,
            Proxy = Proxy,
            ProxyCredential = ProxyCredential,
            BlockResourceType = BlockResourceType,
            BlockResourcePattern = BlockResourcePattern
        }, cancellationToken).ConfigureAwait(false);
    }
}
