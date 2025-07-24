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
[Alias("Get-HTMLLoginForm")]
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

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlFormLogin? result;
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                    Url!,
                    Browser,
                    Clean.IsPresent,
                    username: null,
                    password: null,
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
                    username: null,
                    password: null,
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

        if (result != null) {
            WriteObject(result);
        }
    }
}