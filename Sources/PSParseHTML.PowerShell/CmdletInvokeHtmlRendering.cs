using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
/// <example>
/// <code>Invoke-HTMLRendering -Url https://example.com -Browser Chromium -Clean</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLRendering", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(string))]
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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string? user = Credential?.UserName ?? Username;
        string? pass = Credential?.GetNetworkCredential().Password ?? Password;

        if (!string.IsNullOrEmpty(OutFile)) {
            await HtmlBrowserRenderer.SavePageContentAsync(Url, OutFile, Browser, Clean.IsPresent, user, pass).ConfigureAwait(false);
        } else {
            string html = await HtmlBrowserRenderer.GetPageContentAsync(Url, Browser, Clean.IsPresent, user, pass).ConfigureAwait(false);
            WriteObject(html);
        }
    }
}
