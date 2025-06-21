using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that attempts to detect a login form and returns selectors for its fields.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLLoginForm", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlFormLogin))]
public sealed class CmdletGetHtmlLoginForm : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Browser session to inspect.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL of the page to inspect.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Browser engine to use when loading <see cref="Url"/> or <see cref="Path"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

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
    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <summary>Credentials for pages requiring authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? Credential { get; set; }

    /// <summary>Basic authentication username.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Username { get; set; }

    /// <summary>Basic authentication password.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Password { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlFormLogin? result;
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                string? user = Credential?.UserName ?? Username;
                string? pass = Credential?.GetNetworkCredential().Password ?? Password;
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                    Url!,
                    Browser,
                    Clean.IsPresent,
                    user,
                    pass,
                    formLogin: null,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    storageStatePath: null,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass,
                    timeout: Timeout,
                    cancellationToken: token).ConfigureAwait(false)) {
                    result = await HtmlBrowser.DetectLoginFormAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            case ParameterSetFile:
                string fileUrl = new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri;
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                    fileUrl,
                    Browser,
                    Clean.IsPresent,
                    null,
                    null,
                    formLogin: null,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    storageStatePath: null,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass,
                    timeout: Timeout,
                    cancellationToken: token).ConfigureAwait(false)) {
                    result = await HtmlBrowser.DetectLoginFormAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            default:
                HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                result = await HtmlBrowser.DetectLoginFormAsync(session.Page, token).ConfigureAwait(false);
                break;
        }
        WriteObject(result);
    }
}
