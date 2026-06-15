using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that starts recording a browser session to a WebM file.
/// </summary>
[Cmdlet(VerbsLifecycle.Start, "HtmlBrowserVideoCapture", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Start-HtmlVideoRecording")]
public sealed class CmdletStartHtmlBrowserVideoCapture : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";
    private const string ParameterSetSession = "Session";

    /// <summary>URL of the page to record.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to an HTML file to open.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Existing browser session to record.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Credentials used for login.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? Credential { get; set; }

    /// <summary>Username for basic authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Username { get; set; }

    /// <summary>Password for basic authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Password { get; set; }

    /// <summary>Login page URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit button.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? SubmitSelector { get; set; }

    /// <summary>Path where the WebM file will be stored.</summary>
    [Parameter(Mandatory = true)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Engine to use when creating a new session.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Remove previous session data.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show browser window instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Delay between Playwright actions in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Browser window width.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Width { get; set; } = 800;

    /// <summary>Browser window height.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Height { get; set; } = 600;

    /// <summary>Custom User-Agent header.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Viewport width override.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height override.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportHeight { get; set; }

    /// <summary>Device scale factor for emulation.</summary>
    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    /// <summary>Latitude of the emulated geolocation.</summary>
    [Parameter]
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude of the emulated geolocation.</summary>
    [Parameter]
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier.</summary>
    [Parameter]
    public string? Timezone { get; set; }

    /// <summary>Do not store the created session in <c>PSParseHTML_DefaultSession</c>.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!OutFile.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) {
            throw new PSArgumentException("Only .webm files are supported.", nameof(OutFile));
        }
        string target;
        HtmlBrowserEngine engine = Browser;
        bool clean = Clean.IsPresent;
        bool headless = !Visible.IsPresent;
        string? user = null;
        string? pass = null;
        HtmlFormLogin? form = null;

        switch (ParameterSetName) {
            case ParameterSetSession:
                Session ??= (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                target = Session.Page.Url;
                string browserType = Session.Browser.BrowserType.Name;
                engine = browserType switch {
                    "firefox" => HtmlBrowserEngine.Firefox,
                    "webkit" => HtmlBrowserEngine.WebKit,
                    _ => HtmlBrowserEngine.Chromium
                };
                break;
            case ParameterSetFile:
                target = new System.Uri(Path!.ToFullPath()).AbsoluteUri;
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
                Height,
                UserAgent,
                ViewportWidth,
                ViewportHeight,
                (float?)DeviceScaleFactor,
                GeoLatitude,
                GeoLongitude,
                Timezone).ConfigureAwait(false)
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
                UserAgent,
                ViewportWidth,
                ViewportHeight,
                (float?)DeviceScaleFactor,
                GeoLatitude,
                GeoLongitude,
                Timezone).ConfigureAwait(false);

        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
        }

        WriteObject(sess);
    }
}
