using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns interactive elements from an active browser session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLInteractable", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlInteractableInfo))]
public sealed class CmdletGetHtmlInteractable : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";
    /// <summary>Browser session containing the page.</summary>
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
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Timeout in milliseconds for browser operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <summary>Include elements hidden from view.</summary>
    [Parameter]
    public SwitchParameter IncludeHidden { get; set; }

    /// <summary>Maximum number of elements to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 100;

    /// <summary>Credentials for pages requiring authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? Credential { get; set; }

    /// <summary>Basic authentication username.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Username { get; set; }

    /// <summary>Basic authentication password.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Password { get; set; }

    /// <summary>URL of a login form.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit element.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? SubmitSelector { get; set; }

    /// <summary>Optional case-insensitive filter applied to the element text.</summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlInteractableInfo> list;
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                string? user = Credential?.UserName ?? Username;
                string? pass = Credential?.GetNetworkCredential().Password ?? Password;
                HtmlFormLogin? form = null;
                if (!string.IsNullOrEmpty(LoginUrl) &&
                    !string.IsNullOrEmpty(UsernameSelector) &&
                    !string.IsNullOrEmpty(PasswordSelector) &&
                    !string.IsNullOrEmpty(SubmitSelector)) {
                    form = new HtmlFormLogin {
                        LoginUrl = LoginUrl!,
                        UsernameSelector = UsernameSelector!,
                        PasswordSelector = PasswordSelector!,
                        SubmitSelector = SubmitSelector!
                    };
                }

                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                    Url!,
                    Browser,
                    Clean.IsPresent,
                    user,
                    pass,
                    form,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    storageStatePath: null,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass,
                    timeout: Timeout,
                    cancellationToken: token).ConfigureAwait(false)) {
                    list = await HtmlBrowser.GetInteractablesAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            case ParameterSetFile:
                string fileUrl = new Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri;
                await using (HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                    fileUrl,
                    Browser,
                    Clean.IsPresent,
                    null,
                    null,
                    null,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    storageStatePath: null,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass,
                    timeout: Timeout,
                    cancellationToken: token).ConfigureAwait(false)) {
                    list = await HtmlBrowser.GetInteractablesAsync(sess.Page, token).ConfigureAwait(false);
                }
                break;
            default:
                HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                list = await HtmlBrowser.GetInteractablesAsync(session.Page, token).ConfigureAwait(false);
                break;
        }

        if (!IncludeHidden.IsPresent) {
            list = list.FindAll(x => x.Visible);
        }
        if (!string.IsNullOrEmpty(Filter)) {
            list = list.FindAll(x => x.Text.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        if (Limit > 0) {
            list = list.Take(Limit).ToList();
        }

        WriteObject(list, true);
    }
}