using System.Management.Automation;
using System.Threading.Tasks;
using System.IO;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
/// <example>
/// <code>Invoke-HTMLRendering -Url https://example.com -Browser Chromium -Clean</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLRendering", DefaultParameterSetName = ParameterSetDefault)]
[Alias("Start-HTMLSession", "Open-HTMLSession")]
[OutputType(typeof(string), typeof(HtmlBrowserSession))]
public sealed class CmdletInvokeHtmlRendering : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetFile = "File";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Optional file path to save the rendered HTML.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Credentials used when accessing authenticated pages.</summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Username for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Username { get; set; }

    /// <summary>Password for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Password { get; set; }

    /// <summary>URL for login form when using form authentication.</summary>
    [Parameter]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field of the login form.</summary>
    [Parameter]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field of the login form.</summary>
    [Parameter]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit element of the login form.</summary>
    [Parameter]
    public string? SubmitSelector { get; set; }

    /// <summary>Return a browser session instead of HTML.</summary>
    [Parameter]
    public SwitchParameter Session { get; set; }

    /// <summary>Do not set the opened session as the default session.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    [Parameter]
    public string? UserAgent { get; set; }

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int? ViewportWidth { get; set; }

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int? ViewportHeight { get; set; }

    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    [Parameter]
    public double? GeoLatitude { get; set; }

    [Parameter]
    public double? GeoLongitude { get; set; }

    [Parameter]
    public string? Timezone { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string? user = Credential?.UserName ?? Username;
        string? pass = Credential?.GetNetworkCredential().Password ?? Password;
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;
        HtmlFormLogin? form = null;
        if (!string.IsNullOrEmpty(LoginUrl) && !string.IsNullOrEmpty(UsernameSelector) && !string.IsNullOrEmpty(PasswordSelector) && !string.IsNullOrEmpty(SubmitSelector)) {
            form = new HtmlFormLogin {
                LoginUrl = LoginUrl!,
                UsernameSelector = UsernameSelector!,
                PasswordSelector = PasswordSelector!,
                SubmitSelector = SubmitSelector!
            };
        }

        string target = ParameterSetName == ParameterSetFile
            ? new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri
            : Url;

        if (Session.IsPresent) {
            HtmlBrowserSession sess = await HtmlBrowser.OpenSessionAsync(
                target,
                Browser,
                Clean.IsPresent,
                user,
                pass,
                form,
                headless: !Visible.IsPresent,
                slowMo: SlowMo,
                storageStatePath: null,
                userAgent: UserAgent,
                viewportWidth: ViewportWidth,
                viewportHeight: ViewportHeight,
                deviceScaleFactor: (float?)DeviceScaleFactor,
                proxy: Proxy,
                proxyUsername: pUser,
                proxyPassword: pPass,
                geoLatitude: GeoLatitude,
                geoLongitude: GeoLongitude,
                timezone: Timezone).ConfigureAwait(false);
            if (!NoDefault.IsPresent) {
                SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
            }
            WriteObject(sess);
        } else if (!string.IsNullOrEmpty(OutFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutFile!);
            await HtmlBrowser.SavePageContentAsync(target, outPath, Browser, Clean.IsPresent, user, pass, form, !Visible.IsPresent, SlowMo, UserAgent, ViewportWidth, ViewportHeight, (float?)DeviceScaleFactor, Proxy, pUser, pPass, GeoLatitude, GeoLongitude, Timezone).ConfigureAwait(false);
        } else {
            string html = await HtmlBrowser.GetPageContentAsync(target, Browser, Clean.IsPresent, user, pass, form, !Visible.IsPresent, SlowMo, UserAgent, ViewportWidth, ViewportHeight, (float?)DeviceScaleFactor, Proxy, pUser, pPass, GeoLatitude, GeoLongitude, Timezone).ConfigureAwait(false);
            WriteObject(html);
        }
    }
}
