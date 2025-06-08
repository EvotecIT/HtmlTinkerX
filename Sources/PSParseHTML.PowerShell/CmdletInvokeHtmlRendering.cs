using System.Management.Automation;
using System.Threading.Tasks;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
/// <example>
/// <code>Invoke-HTMLRendering -Url https://example.com -Browser Chromium -Clean</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLRendering", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(string), typeof(BrowserSession))]
public sealed class CmdletInvokeHtmlRendering : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional file path to save the rendered HTML.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter]
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

        if (Session.IsPresent) {
            BrowserSession sess = await HtmlBrowserRenderer.OpenSessionAsync(
                Url,
                Browser,
                Clean.IsPresent,
                user,
                pass,
                form).ConfigureAwait(false);
            WriteObject(sess);
        } else if (!string.IsNullOrEmpty(OutFile)) {
            await HtmlBrowserRenderer.SavePageContentAsync(Url, OutFile, Browser, Clean.IsPresent, user, pass, form).ConfigureAwait(false);
        } else {
            string html = await HtmlBrowserRenderer.GetPageContentAsync(Url, Browser, Clean.IsPresent, user, pass, form).ConfigureAwait(false);
            WriteObject(html);
        }
    }
}
