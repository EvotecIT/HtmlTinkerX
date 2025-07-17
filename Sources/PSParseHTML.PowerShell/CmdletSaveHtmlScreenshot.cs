using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;
using System.IO;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that captures a screenshot of a web page using a headless browser. If <c>OutFile</c> has no extension, one is added based on <see cref="Format"/>.
/// </summary>
/// <example>
/// <code>Save-HTMLScreenshot -Url https://example.com -OutFile page.png</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLScreenshot", DefaultParameterSetName = ParameterSetSessionDefault)]
public sealed class CmdletSaveHtmlScreenshot : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetClip = "Clip";
    private const string ParameterSetFileDefault = "FileDefault";
    private const string ParameterSetFileClip = "FileClip";
    private const string ParameterSetSessionDefault = "SessionDefault";
    private const string ParameterSetSessionClip = "SessionClip";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetClip)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFileDefault)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFileClip)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSessionDefault, ValueFromPipeline = true)]
    [Parameter(Position = 0, ParameterSetName = ParameterSetSessionClip, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>File path for the screenshot.</summary>
    [Parameter(Position = 1)]
    public string? OutFile { get; set; }

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Open the screenshot after saving.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <summary>Capture the entire page.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetSessionDefault)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    public SwitchParameter Full { get; set; }

    /// <summary>Milliseconds to wait after the page loads.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Delay { get; set; } = 0;

    /// <summary>CSS selector to wait for before capturing.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>CSS selector of an element to capture.</summary>
    [Parameter]
    public string? ElementSelector { get; set; }

    /// <summary>CSS selectors of elements to highlight.</summary>
    [Parameter]
    public string[]? HighlightSelector { get; set; }

    /// <summary>Text to overlay on the screenshot.</summary>
    [Parameter]
    public string? OverlayText { get; set; }

    /// <summary>Image format for the screenshot.</summary>
    [Parameter]
    public ImageFormat Format { get; set; } = ImageFormat.Png;

    /// <summary>Encoder quality for JPEG output.</summary>
    [Parameter]
    [ValidateRange(1,100)]
    public int Quality { get; set; } = 100;

    /// <summary>X coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFileClip)]
    public int X { get; set; }

    /// <summary>Y coordinate for a clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFileClip)]
    public int Y { get; set; }

    /// <summary>Width of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFileClip)]
    public int Width { get; set; }

    /// <summary>Height of the clip region.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionClip)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFileClip)]
    public int Height { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;

        if (string.IsNullOrWhiteSpace(OutFile)) {
            if (Open.IsPresent) {
                OutFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".png");
            } else {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException("OutFile is required unless -Open is specified."),
                    "MissingOutFile",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }
        }

        if (!System.IO.Path.HasExtension(OutFile)) {
            string ext = Format switch {
                ImageFormat.Jpeg => ".jpg",
                ImageFormat.Bmp => ".bmp",
                ImageFormat.Gif => ".gif",
                _ => ".png"
            };
            OutFile += ext;
        }

        if (ParameterSetName.EndsWith("Clip", System.StringComparison.OrdinalIgnoreCase)) {
            if (X < 0 || Y < 0) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentOutOfRangeException(null, null, "Clip coordinates must be zero or positive."),
                    "ClipCoordinateOutOfRange",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }
            if (Width <= 0 || Height <= 0) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentOutOfRangeException(null, null, "Clip dimensions must be greater than zero."),
                    "ClipSizeOutOfRange",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }
        }

        switch (ParameterSetName) {
            case ParameterSetClip:
                await HtmlBrowser.CaptureScreenshotAsync(
                    Url,
                    HtmlUtilities.ResolvePath(OutFile!),
                    Browser,
                    Clean.IsPresent,
                    false,
                    Delay,
                    Format,
                    Quality,
                    Selector,
                    ElementSelector,
                    X,
                    Y,
                    Width,
                    Height,
                    highlightSelectors: HighlightSelector,
                    overlayText: OverlayText,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass).ConfigureAwait(false);
                break;
            case ParameterSetFileClip:
                await HtmlBrowser.CaptureScreenshotAsync(
                    new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri,
                    HtmlUtilities.ResolvePath(OutFile!),
                    Browser,
                    Clean.IsPresent,
                    false,
                    Delay,
                    Format,
                    Quality,
                    Selector,
                    ElementSelector,
                    X,
                    Y,
                    Width,
                    Height,
                    highlightSelectors: HighlightSelector,
                    overlayText: OverlayText,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass).ConfigureAwait(false);
                break;
            case ParameterSetSessionClip:
                await HtmlBrowser.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    HtmlUtilities.ResolvePath(OutFile!),
                    false,
                    Delay,
                    Format,
                    Quality,
                    Selector,
                    ElementSelector,
                    X,
                    Y,
                    Width,
                    Height,
                    highlightSelectors: HighlightSelector,
                    overlayText: OverlayText).ConfigureAwait(false);
                break;
            case ParameterSetSessionDefault:
                await HtmlBrowser.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    HtmlUtilities.ResolvePath(OutFile!),
                    Full.IsPresent,
                    Delay,
                    Format,
                    Quality,
                    Selector,
                    ElementSelector,
                    highlightSelectors: HighlightSelector,
                    overlayText: OverlayText).ConfigureAwait(false);
                break;
            default:
                string target = ParameterSetName == ParameterSetFileDefault
                    ? new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri
                    : Url;
                await HtmlBrowser.CaptureScreenshotAsync(
                    target,
                    HtmlUtilities.ResolvePath(OutFile!),
                    Browser,
                    Clean.IsPresent,
                    Full.IsPresent,
                    Delay,
                    Format,
                    Quality,
                    Selector,
                    ElementSelector,
                    highlightSelectors: HighlightSelector,
                    overlayText: OverlayText,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo,
                    proxy: Proxy,
                    proxyUsername: pUser,
                    proxyPassword: pPass).ConfigureAwait(false);
                break;
        }

        if (Open.IsPresent) {
            try {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = HtmlUtilities.ResolvePath(OutFile!),
                    UseShellExecute = true,
                });
            } catch (System.Exception ex) {
                WriteVerbose($"Failed to open file '{OutFile}': {ex.Message}");
            }
        }
    }
}
