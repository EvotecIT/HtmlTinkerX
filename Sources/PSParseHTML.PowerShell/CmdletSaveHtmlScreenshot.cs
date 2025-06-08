using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that captures a screenshot of a web page using a headless browser.
/// </summary>
/// <example>
/// <code>Save-HTMLScreenshot -Url https://example.com -OutFile page.png</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLScreenshot", DefaultParameterSetName = ParameterSetDefault)]
public sealed class CmdletSaveHtmlScreenshot : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>File path for the screenshot.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        await HtmlBrowserRenderer.CaptureScreenshotAsync(Url, OutFile, Browser, Clean.IsPresent).ConfigureAwait(false);
    }
}
