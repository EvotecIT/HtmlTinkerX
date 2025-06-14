using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsLifecycle.Start, "HTMLVideoRecording", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletStartHtmlVideoRecording : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";
    private const string ParameterSetSession = "Session";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string Url { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? Credential { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Username { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Password { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? LoginUrl { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UsernameSelector { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? PasswordSelector { get; set; }

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? SubmitSelector { get; set; }

    [Parameter(Mandatory = true)]
    public string OutFile { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    [Parameter]
    public SwitchParameter Visible { get; set; }

    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>
    /// Proxy server address used for browser traffic.
    /// Include protocol and port if required.
    /// </summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>
    /// Credentials used for the specified <see cref="Proxy"/> server.
    /// </summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int Width { get; set; } = 800;

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int Height { get; set; } = 600;

    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    protected override async Task ProcessRecordAsync() {
        if (System.IO.Path.GetExtension(OutFile) != ".webm") {
            throw new PSArgumentException("Only .webm files are supported.", nameof(OutFile));
        }
        string target;
        HtmlBrowserEngine engine = Browser;
        bool clean = Clean.IsPresent;
        bool headless = !Visible.IsPresent;
        string? user = null;
        string? pass = null;
        string? proxyUser = ProxyCredential?.UserName;
        string? proxyPass = ProxyCredential?.GetNetworkCredential().Password;
        HtmlFormLogin? form = null;

        switch (ParameterSetName) {
            case ParameterSetSession:
                Session ??= (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                target = Session.Page.Url;
                engine = Session.Browser.BrowserType.Name switch {
                    "firefox" => HtmlBrowserEngine.Firefox,
                    "webkit" => HtmlBrowserEngine.Webkit,
                    _ => HtmlBrowserEngine.Chromium
                };
                break;
            case ParameterSetFile:
                target = new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri;
                break;
            default:
                target = Url;
                break;
        }

        if (ParameterSetName != ParameterSetSession) {
            user = Credential?.UserName ?? Username;
            pass = Credential?.GetNetworkCredential().Password ?? Password;
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
        }

        HtmlBrowserSession sess = ParameterSetName == ParameterSetSession
            ? await HtmlBrowser.StartVideoRecordingAsync(
                Session!,
                OutFile,
                headless,
                SlowMo,
                Width,
                Height).ConfigureAwait(false)
            : await HtmlBrowser.StartVideoRecordingAsync(
                target,
                OutFile,
                engine,
                clean,
                user,
                pass,
                form,
                headless,
                SlowMo,
                Width,
                Height,
                proxy: Proxy,
                proxyUsername: proxyUser,
                proxyPassword: proxyPass).ConfigureAwait(false);

        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
        }

        WriteObject(sess);
    }
}
