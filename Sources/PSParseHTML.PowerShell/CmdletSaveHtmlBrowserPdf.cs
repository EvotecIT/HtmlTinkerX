using HtmlTinkerX;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that generates a PDF from a web page using a headless browser.
/// </summary>
/// <example>
///   <code>Save-HTMLPdf -Url https://example.com -OutFile page.pdf</code>
/// </example>
/// <example>
///   <code>Invoke-HtmlRendering -Url https://example.com -Session |
///   Save-HTMLPdf -OutFile page.pdf</code>
/// </example>
[Cmdlet(VerbsData.Save, "HtmlBrowserPdf", DefaultParameterSetName = ParameterSetSession)]
[Alias("Save-HtmlPdf")]
public sealed class CmdletSaveHtmlBrowserPdf : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetFile = "File";
    private const string ParameterSetSession = "Session";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>File path for the PDF.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Optional browser profile JSON file used as launch defaults for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? ProfilePath { get; set; }

    /// <summary>Intent-focused browser automation defaults used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Persistent browser user-data directory used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? BrowserChannel { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Ignore HTTPS certificate errors for a trusted URL or file source.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter IgnoreHttpsErrors { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Initial browser navigation readiness state for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Timeout in milliseconds for navigation and browser operations.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Open the PDF after saving.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <summary>Milliseconds to wait after the page loads.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Delay { get; set; } = 0;

    /// <summary>CSS selector to wait for before generating the PDF.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>CSS selectors of elements to mask before generating the PDF.</summary>
    [Parameter]
    public string[]? MaskSelector { get; set; }

    /// <summary>Mask common sensitive fields such as password, token, SAML, MFA, OTP, and secret inputs before generating the PDF.</summary>
    [Parameter]
    public SwitchParameter MaskSensitiveElement { get; set; }

    /// <summary>CSS color used for masked elements.</summary>
    [Parameter]
    public string? MaskColor { get; set; }

    /// <summary>Render the page in landscape orientation.</summary>
    [Parameter]
    public SwitchParameter Landscape { get; set; }

    /// <summary>Include background graphics.</summary>
    [Parameter]
    public SwitchParameter PrintBackground { get; set; }

    /// <summary>Paper format (e.g. A4).</summary>
    [Parameter]
    public PdfPageFormat? Format { get; set; }

    /// <summary>Paper width (e.g. 8.5in).</summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>Paper height (e.g. 11in).</summary>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>Top margin.</summary>
    [Parameter]
    public string? MarginTop { get; set; }

    /// <summary>Right margin.</summary>
    [Parameter]
    public string? MarginRight { get; set; }

    /// <summary>Bottom margin.</summary>
    [Parameter]
    public string? MarginBottom { get; set; }

    /// <summary>Left margin.</summary>
    [Parameter]
    public string? MarginLeft { get; set; }

    /// <summary>Page ranges to print.</summary>
    [Parameter]
    public string? PageRanges { get; set; }

    /// <summary>Scaling factor.</summary>
    [Parameter]
    [ValidateRange(0.1, 2.0)]
    public float? Scale { get; set; }

    /// <summary>Display headers and footers.</summary>
    [Parameter]
    public SwitchParameter DisplayHeaderFooter { get; set; }

    /// <summary>Header HTML template.</summary>
    [Parameter]
    public string? HeaderTemplate { get; set; }

    /// <summary>Footer HTML template.</summary>
    [Parameter]
    public string? FooterTemplate { get; set; }

    /// <summary>Prefer CSS @page size rules.</summary>
    [Parameter]
    public SwitchParameter PreferCssPageSize { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");
        string outPath = OutFile.ToFullPath();
        string? dir = System.IO.Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        switch (ParameterSetName) {
            case ParameterSetSession:
                await HtmlBrowser.SavePagePdfAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    outPath,
                    CreatePdfOptions(),
                    CreatePdfReadiness(),
                    token).ConfigureAwait(false);
                break;
            case ParameterSetFile:
                await SaveOneShotAsync(new System.Uri(System.IO.Path.GetFullPath(Path!)).AbsoluteUri, outPath, token).ConfigureAwait(false);
                break;
            default:
                await SaveOneShotAsync(Url, outPath, token).ConfigureAwait(false);
                break;
        }

        if (Open.IsPresent) {
            Process.Start(new ProcessStartInfo {
                FileName = outPath,
                UseShellExecute = true,
            });
        }
    }

    private async Task SaveOneShotAsync(string target, string outPath, CancellationToken cancellationToken) {
        HtmlBrowserLaunchOptions launchOptions = await CreateLaunchOptionsAsync(cancellationToken).ConfigureAwait(false);
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(target, launchOptions, cancellationToken).ConfigureAwait(false);
        await HtmlBrowser.SavePagePdfAsync(
            session.Page,
            outPath,
            CreatePdfOptions(),
            CreatePdfReadiness(),
            cancellationToken).ConfigureAwait(false);
    }

    private HtmlBrowserPdfOptions CreatePdfOptions() {
        return new HtmlBrowserPdfOptions(
            landscape: Landscape.IsPresent,
            printBackground: PrintBackground.IsPresent,
            format: Format,
            width: Width,
            height: Height,
            marginTop: MarginTop,
            marginRight: MarginRight,
            marginBottom: MarginBottom,
            marginLeft: MarginLeft,
            pageRanges: PageRanges,
            scale: Scale,
            displayHeaderFooter: DisplayHeaderFooter.IsPresent,
            headerTemplate: HeaderTemplate,
            footerTemplate: FooterTemplate,
            preferCssPageSize: PreferCssPageSize.IsPresent,
            maskSensitiveElements: MaskSensitiveElement.IsPresent,
            maskSelectors: MaskSelector,
            maskColor: MaskColor);
    }

    private HtmlBrowserPdfReadiness? CreatePdfReadiness() {
        if (Delay == 0 && string.IsNullOrWhiteSpace(Selector)) return null;
        return new HtmlBrowserPdfReadiness(
            skipLoadState: true,
            selector: Selector,
            timeout: 10000,
            delayMilliseconds: Delay);
    }

    private async Task<HtmlBrowserLaunchOptions> CreateLaunchOptionsAsync(CancellationToken cancellationToken) {
        return await HtmlBrowserLaunchOptionFactory.CreateAsync(new HtmlBrowserLaunchOptionRequest {
            BoundParameters = MyInvocation.BoundParameters,
            ProfilePath = ProfilePath,
            Scenario = Scenario,
            Browser = Browser,
            Clean = Clean,
            Visible = Visible,
            IgnoreHttpsErrors = IgnoreHttpsErrors,
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
