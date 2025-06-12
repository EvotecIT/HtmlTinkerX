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
[OutputType(typeof(string), typeof(BrowserSession))]
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
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string? user = Credential?.UserName ?? Username;
        string? pass = Credential?.GetNetworkCredential().Password ?? Password;
        FormLoginOptions? form = null;
        if (!string.IsNullOrEmpty(LoginUrl) && !string.IsNullOrEmpty(UsernameSelector) && !string.IsNullOrEmpty(PasswordSelector) && !string.IsNullOrEmpty(SubmitSelector)) {
            form = new FormLoginOptions {
                LoginUrl = LoginUrl!,
                UsernameSelector = UsernameSelector!,
                PasswordSelector = PasswordSelector!,
                SubmitSelector = SubmitSelector!
            };
        }

        string target = ParameterSetName == ParameterSetFile
            ? new System.Uri(FileUtilities.ResolvePath(Path!)).AbsoluteUri
            : Url;

        if (Session.IsPresent) {
            BrowserSession sess = await HtmlBrowserRenderer.OpenSessionAsync(
                target,
                Browser,
                Clean.IsPresent,
                user,
                pass,
                form).ConfigureAwait(false);
            if (!NoDefault.IsPresent) {
                SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
            }
            WriteObject(sess);
        } else if (!string.IsNullOrEmpty(OutFile)) {
            string outPath = FileUtilities.ResolvePath(OutFile);
            await HtmlBrowserRenderer.SavePageContentAsync(target, outPath, Browser, Clean.IsPresent, user, pass, form).ConfigureAwait(false);
        } else {
            string html = await HtmlBrowserRenderer.GetPageContentAsync(target, Browser, Clean.IsPresent, user, pass, form).ConfigureAwait(false);
            WriteObject(html);
        }
    }
}
