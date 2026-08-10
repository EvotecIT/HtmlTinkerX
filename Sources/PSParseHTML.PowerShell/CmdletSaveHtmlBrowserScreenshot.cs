using HtmlTinkerX;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that captures a screenshot of a web page using a headless browser. If <c>OutFile</c> has no extension, one is added based on <see cref="Format"/>.
/// </summary>
/// <example>
/// <code>Save-HtmlScreenshot -Url https://example.com -OutFile page.png</code>
/// </example>
[Cmdlet(VerbsData.Save, "HtmlBrowserScreenshot", DefaultParameterSetName = ParameterSetSessionDefault)]
[Alias("Save-HtmlScreenshot")]
public sealed class CmdletSaveHtmlBrowserScreenshot : AsyncPSCmdlet {
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

    /// <summary>Optional browser profile JSON file used as launch defaults for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public string? ProfilePath { get; set; }

    /// <summary>Intent-focused browser automation defaults used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Persistent browser user-data directory used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public string? BrowserChannel { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Initial browser navigation readiness state for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Timeout in milliseconds for navigation and browser operations.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetClip)]
    [Parameter(ParameterSetName = ParameterSetFileDefault)]
    [Parameter(ParameterSetName = ParameterSetFileClip)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

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

    /// <summary>CSS selectors of elements to mask.</summary>
    [Parameter]
    public string[]? MaskSelector { get; set; }

    /// <summary>Mask common sensitive fields such as password, token, SAML, MFA, OTP, and secret inputs.</summary>
    [Parameter]
    public SwitchParameter MaskSensitiveElement { get; set; }

    /// <summary>CSS color used for masked elements.</summary>
    [Parameter]
    public string? MaskColor { get; set; }

    /// <summary>Text to overlay on the screenshot.</summary>
    [Parameter]
    public string? OverlayText { get; set; }

    /// <summary>Image format for the screenshot.</summary>
    [Parameter]
    public ImageFormat Format { get; set; } = ImageFormat.Png;

    /// <summary>Encoder quality for JPEG output.</summary>
    [Parameter]
    [ValidateRange(1, 100)]
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

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");

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

        bool clip = ParameterSetName.EndsWith("Clip", System.StringComparison.OrdinalIgnoreCase);
        ScreenshotOptions options = new() {
            FullPage = !clip && Full.IsPresent,
            DelayMs = Delay,
            Format = Format,
            Quality = Quality,
            Selector = Selector,
            ElementSelector = ElementSelector,
            HighlightSelectors = HighlightSelector,
            MaskSelectors = MaskSelector,
            MaskSensitiveElements = MaskSensitiveElement.IsPresent,
            MaskColor = MaskColor,
            OverlayText = OverlayText
        };
        if (clip) {
            options.ClipX = X;
            options.ClipY = Y;
            options.ClipWidth = Width;
            options.ClipHeight = Height;
        }

        switch (ParameterSetName) {
            case ParameterSetClip:
                await CaptureOneShotAsync(Url, options, token).ConfigureAwait(false);
                break;
            case ParameterSetFileClip:
                await CaptureOneShotAsync(HtmlBrowser.CreateLocalFileUri(Path!).AbsoluteUri, options, token).ConfigureAwait(false);
                break;
            case ParameterSetSessionClip:
                await HtmlBrowser.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    OutFile!.ToFullPath(),
                    options,
                    token).ConfigureAwait(false);
                break;
            case ParameterSetSessionDefault:
                await HtmlBrowser.CaptureScreenshotAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    OutFile!.ToFullPath(),
                    options,
                    token).ConfigureAwait(false);
                break;
            default:
                string target = ParameterSetName == ParameterSetFileDefault
                    ? HtmlBrowser.CreateLocalFileUri(Path!).AbsoluteUri
                    : Url;
                await CaptureOneShotAsync(target, options, token).ConfigureAwait(false);
                break;
        }

        if (Open.IsPresent) {
            try {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = OutFile!.ToFullPath(),
                    UseShellExecute = true,
                });
            } catch (System.Exception ex) {
                WriteVerbose($"Failed to open file '{OutFile}': {ex.Message}");
            }
        }
    }

    private async Task CaptureOneShotAsync(string target, ScreenshotOptions options, CancellationToken cancellationToken) {
        HtmlBrowserLaunchOptions launchOptions = await CreateLaunchOptionsAsync(cancellationToken).ConfigureAwait(false);
        await HtmlBrowser.CaptureScreenshotAsync(
            target,
            OutFile!.ToFullPath(),
            launchOptions,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HtmlBrowserLaunchOptions> CreateLaunchOptionsAsync(CancellationToken cancellationToken) {
        return await HtmlBrowserLaunchOptionFactory.CreateAsync(new HtmlBrowserLaunchOptionRequest {
            BoundParameters = MyInvocation.BoundParameters,
            ProfilePath = ProfilePath,
            Scenario = Scenario,
            Browser = Browser,
            Clean = Clean,
            Visible = Visible,
            SlowMo = SlowMo,
            Timeout = Timeout,
            LoadState = LoadState,
            UserDataDirectory = UserDataDirectory,
            StatePath = StatePath,
            BrowserChannel = BrowserChannel,
            Proxy = Proxy,
            ProxyCredential = ProxyCredential,
            BlockResourceType = BlockResourceType,
            BlockResourcePattern = BlockResourcePattern
        }, cancellationToken).ConfigureAwait(false);
    }
}
