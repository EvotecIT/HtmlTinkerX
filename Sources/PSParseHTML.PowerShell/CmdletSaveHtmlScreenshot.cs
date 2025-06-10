using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that captures a screenshot of a web page using a headless browser.
/// </summary>
/// <example>
/// <code>Save-HTMLScreenshot -Url https://example.com -OutFile page.png</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLScreenshot", DefaultParameterSetName = ParameterSetSessionDefault)]
public sealed class CmdletSaveHtmlScreenshot : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetClip = "Clip";
    private const string ParameterSetSessionDefault = "SessionDefault";
    private const string ParameterSetSessionClip = "SessionClip";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetClip)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSessionDefault, ValueFromPipeline = true)]
    [Parameter(Position = 0, ParameterSetName = ParameterSetSessionClip, ValueFromPipeline = true)]
    public BrowserSession? Session { get; set; }

    /// <summary>File path for the screenshot.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Open the screenshot after saving.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <summary>Capture the entire page.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetSessionDefault)]
    public SwitchParameter Full { get; set; }

    /// <summary>Milliseconds to wait after the page loads.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Delay { get; set; } = 0;

    /// <summary>CSS selector to wait for before capturing.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>X coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    public int X { get; set; }

    /// <summary>Y coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    public int Y { get; set; }

    /// <summary>Width of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    public int Width { get; set; }

    /// <summary>Height of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    public int Height { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        BrowserSession? session = Session ?? (BrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");

        switch (ParameterSetName) {
            case ParameterSetClip:
                await HtmlBrowserRenderer.CaptureScreenshotAsync(
                    Url,
                    OutFile,
                    Browser,
                    Clean.IsPresent,
                    false,
                    Delay,
                    Selector,
                    X,
                    Y,
                    Width,
                    Height).ConfigureAwait(false);
                break;
            case ParameterSetSessionClip:
                await HtmlBrowserRenderer.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    OutFile,
                    false,
                    Delay,
                    Selector,
                    X,
                    Y,
                    Width,
                    Height).ConfigureAwait(false);
                break;
            case ParameterSetSessionDefault:
                await HtmlBrowserRenderer.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    OutFile,
                    Full.IsPresent,
                    Delay,
                    Selector).ConfigureAwait(false);
                break;
            default:
                await HtmlBrowserRenderer.CaptureScreenshotAsync(
                    Url,
                    OutFile,
                    Browser,
                    Clean.IsPresent,
                    Full.IsPresent,
                    Delay,
                    Selector).ConfigureAwait(false);
                break;
        }

        if (Open.IsPresent) {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = OutFile,
                UseShellExecute = true,
            });
        }
    }
}
