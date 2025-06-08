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
[Cmdlet(VerbsData.Save, "HTMLScreenshot", DefaultParameterSetName = ParameterSetDefault)]
public sealed class CmdletSaveHtmlScreenshot : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetClip = "Clip";

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

    /// <summary>Open the screenshot after saving.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <summary>Capture the entire page.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    public SwitchParameter Full { get; set; }

    /// <summary>X coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    public int X { get; set; }

    /// <summary>Y coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    public int Y { get; set; }

    /// <summary>Width of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    public int Width { get; set; }

    /// <summary>Height of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    public int Height { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == ParameterSetClip) {
            await HtmlBrowserRenderer.CaptureScreenshotAsync(Url, OutFile, Browser, Clean.IsPresent, false, X, Y, Width, Height).ConfigureAwait(false);
        } else {
            await HtmlBrowserRenderer.CaptureScreenshotAsync(Url, OutFile, Browser, Clean.IsPresent, Full.IsPresent).ConfigureAwait(false);
        }

        if (Open.IsPresent) {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = OutFile,
                UseShellExecute = true,
            });
        }
    }
}
