using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Exports screenshot, rendered content, text, Markdown, and optional network evidence from a browser session or URL.
/// </summary>
/// <example>
///   <summary>Export evidence from an interactive browser session</summary>
///   <code>$session | Export-HtmlBrowserEvidence -OutFolder .\evidence -NetworkSummary</code>
/// </example>
/// <example>
///   <summary>Open a URL and export only selected artifacts</summary>
///   <code>Export-HtmlBrowserEvidence -Url https://example.org -OutFolder .\evidence -Artifact Screenshot,Html,Text,NetworkSummary</code>
/// </example>
[Cmdlet(VerbsData.Export, "HtmlBrowserEvidence", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlBrowserEvidenceResult))]
public sealed class CmdletExportHtmlBrowserEvidence : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Browser session to capture. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true, ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL to open for one-shot evidence capture.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Local HTML file to open for one-shot evidence capture.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Output folder for evidence artifacts.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFolder { get; set; } = string.Empty;

    /// <summary>Base file name for page-level artifacts. The default is page.</summary>
    [Parameter]
    public string BaseFileName { get; set; } = "page";

    /// <summary>Specific artifacts to export. When omitted, screenshot, HTML, text, and Markdown are written, with optional additive switches.</summary>
    [Parameter]
    [ValidateSet("Screenshot", "FullPageScreenshot", "Pdf", "Html", "Text", "Markdown", "NetworkSummary", "SsoHandoffSummary")]
    public string[] Artifact { get; set; } = System.Array.Empty<string>();

    /// <summary>Capture the viewport screenshot. This is included in the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter Screenshot { get; set; }

    /// <summary>Add a full-page screenshot to the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter FullPageScreenshot { get; set; }

    /// <summary>Add a PDF print of the page to the default evidence pack. Playwright supports this only for Chromium.</summary>
    [Parameter]
    public SwitchParameter Pdf { get; set; }

    /// <summary>Write rendered page HTML. This is included in the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter Html { get; set; }

    /// <summary>Write visible page text. This is included in the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter VisibleText { get; set; }

    /// <summary>Write Markdown converted from the rendered page HTML. This is included in the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter Markdown { get; set; }

    /// <summary>Add a redacted network summary without headers or bodies to the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter NetworkSummary { get; set; }

    /// <summary>Add a redacted SSO handoff summary to the default evidence pack.</summary>
    [Parameter]
    public SwitchParameter SsoHandoffSummary { get; set; }

    /// <summary>Do not write evidence-manifest.json.</summary>
    [Parameter]
    public SwitchParameter NoManifest { get; set; }

    /// <summary>Write raw text artifacts and manifest URLs without sensitive-value redaction.</summary>
    [Parameter]
    public SwitchParameter NoRedaction { get; set; }

    /// <summary>Do not mask common sensitive fields in visual artifacts such as screenshots and PDFs.</summary>
    [Parameter]
    [Alias("NoVisualMask")]
    public SwitchParameter NoScreenshotMask { get; set; }

    /// <summary>Additional selectors to mask in visual artifacts such as screenshots and PDFs.</summary>
    [Parameter]
    [Alias("VisualMaskSelector")]
    public string[] ScreenshotMaskSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>CSS color used for visual artifact masks.</summary>
    [Parameter]
    [Alias("VisualMaskColor")]
    public string? ScreenshotMaskColor { get; set; }

    /// <summary>Optional browser profile JSON file used as launch defaults for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? ProfilePath { get; set; }

    /// <summary>Persistent browser user-data directory used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? UserDataDirectory { get; set; }

    /// <summary>Playwright storage-state JSON file used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [Alias("StorageStatePath")]
    public string? StatePath { get; set; }

    /// <summary>Browser engine used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Intent-focused browser automation defaults used for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? BrowserChannel { get; set; }

    /// <summary>Show the browser instead of running headless for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting during one-shot URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter PreventSsoAutoSubmit { get; set; }

    /// <summary>Proxy server address used when launching the browser for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Initial browser navigation readiness state for URL or file captures.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Timeout in milliseconds for navigation and browser operations.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        HtmlBrowserEvidenceOptions options = CreateEvidenceOptions();

        if (ParameterSetName == ParameterSetSession) {
            HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                ?? throw new PSInvalidOperationException("No session provided and no default session found.");
            HtmlBrowserEvidenceResult result = await HtmlBrowser.ExportEvidenceAsync(session, OutFolder, options, token).ConfigureAwait(false);
            WriteObject(result);
            return;
        }

        HtmlBrowserLaunchOptions launchOptions = await CreateLaunchOptionsAsync(token).ConfigureAwait(false);
        if (PreventSsoAutoSubmit.IsPresent || ShouldCaptureSsoHandoffSummary()) {
            launchOptions.PreventSsoAutoSubmit = true;
        }

        string target = ParameterSetName == ParameterSetFile
            ? new System.Uri(Path!.ToFullPath()).AbsoluteUri
            : Url!;

        await using HtmlBrowserSession oneShotSession = await HtmlBrowser.OpenSessionAsync(target, launchOptions, token).ConfigureAwait(false);
        HtmlBrowserEvidenceResult oneShotResult = await HtmlBrowser.ExportEvidenceAsync(oneShotSession, OutFolder, options, token).ConfigureAwait(false);
        WriteObject(oneShotResult);
    }

    private HtmlBrowserEvidenceOptions CreateEvidenceOptions() {
        HtmlBrowserEvidenceOptions options = new() {
            BaseFileName = BaseFileName,
            Manifest = !NoManifest.IsPresent,
            RedactSensitiveValues = !NoRedaction.IsPresent,
            MaskSensitiveScreenshotElements = !NoScreenshotMask.IsPresent,
            ScreenshotMaskColor = ScreenshotMaskColor
        };
        AddRange(options.ScreenshotMaskSelectors, ScreenshotMaskSelector);

        if (Artifact.Length == 0) {
            options.FullPageScreenshot = FullPageScreenshot.IsPresent;
            options.Pdf = Pdf.IsPresent;
            options.NetworkSummary = NetworkSummary.IsPresent;
            options.SsoHandoffSummary = SsoHandoffSummary.IsPresent;
            return options;
        }

        options.Screenshot = false;
        options.FullPageScreenshot = false;
        options.Pdf = false;
        options.Html = false;
        options.VisibleText = false;
        options.Markdown = false;
        options.NetworkSummary = false;
        options.SsoHandoffSummary = false;

        foreach (string artifact in Artifact) {
            switch (artifact) {
                case "Screenshot":
                    options.Screenshot = true;
                    break;
                case "FullPageScreenshot":
                    options.FullPageScreenshot = true;
                    break;
                case "Pdf":
                    options.Pdf = true;
                    break;
                case "Html":
                    options.Html = true;
                    break;
                case "Text":
                    options.VisibleText = true;
                    break;
                case "Markdown":
                    options.Markdown = true;
                    break;
                case "NetworkSummary":
                    options.NetworkSummary = true;
                    break;
                case "SsoHandoffSummary":
                    options.SsoHandoffSummary = true;
                    break;
            }
        }

        return options;
    }

    private bool ShouldCaptureSsoHandoffSummary() {
        if (SsoHandoffSummary.IsPresent) {
            return true;
        }

        foreach (string artifact in Artifact) {
            if (string.Equals(artifact, "SsoHandoffSummary", System.StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private async Task<HtmlBrowserLaunchOptions> CreateLaunchOptionsAsync(CancellationToken cancellationToken) {
        return await HtmlBrowserLaunchOptionFactory.CreateAsync(new HtmlBrowserLaunchOptionRequest {
            BoundParameters = MyInvocation.BoundParameters,
            ProfilePath = ProfilePath,
            Scenario = Scenario,
            Browser = Browser,
            Visible = Visible,
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

    private static void AddRange<T>(System.Collections.Generic.ICollection<T> target, System.Collections.Generic.IEnumerable<T>? values) {
        if (values == null) {
            return;
        }

        foreach (T value in values) {
            target.Add(value);
        }
    }
}
