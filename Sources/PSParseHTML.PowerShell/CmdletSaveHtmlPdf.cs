using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that generates a PDF from a web page using a headless browser.
/// </summary>
/// <example>
///   <code>Save-HTMLPdf -Url https://example.com -OutFile page.pdf</code>
/// </example>
/// <example>
///   <code>Invoke-HTMLRendering -Url https://example.com -Session |
///   Save-HTMLPdf -OutFile page.pdf</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLPdf", DefaultParameterSetName = ParameterSetSession)]
public sealed class CmdletSaveHtmlPdf : AsyncPSCmdlet {
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

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

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

    /// <summary>Render the page in landscape orientation.</summary>
    [Parameter]
    public SwitchParameter Landscape { get; set; }

    /// <summary>Include background graphics.</summary>
    [Parameter]
    public SwitchParameter PrintBackground { get; set; }

    /// <summary>Paper format (e.g. A4).</summary>
    [Parameter]
    public string? Format { get; set; }

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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");
        switch (ParameterSetName) {
            case ParameterSetSession:
                await HtmlBrowser.SavePagePdfAsync(
                    (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                    OutFile,
                    Delay,
                    Selector,
                    Landscape.IsPresent,
                    PrintBackground.IsPresent,
                    Format,
                    Width,
                    Height,
                    MarginTop,
                    MarginRight,
                    MarginBottom,
                    MarginLeft,
                    PageRanges,
                    Scale,
                    DisplayHeaderFooter.IsPresent,
                    HeaderTemplate,
                    FooterTemplate,
                    PreferCssPageSize.IsPresent,
                    outline: false,
                    tagged: false).ConfigureAwait(false);
                break;
            case ParameterSetFile:
                await HtmlBrowser.SavePagePdfAsync(
                    new System.Uri(System.IO.Path.GetFullPath(Path!)).AbsoluteUri,
                    OutFile,
                    Browser,
                    Clean.IsPresent,
                    Delay,
                    Selector,
                    Landscape.IsPresent,
                    PrintBackground.IsPresent,
                    Format,
                    Width,
                    Height,
                    MarginTop,
                    MarginRight,
                    MarginBottom,
                    MarginLeft,
                    PageRanges,
                    Scale,
                    DisplayHeaderFooter.IsPresent,
                    HeaderTemplate,
                    FooterTemplate,
                    PreferCssPageSize.IsPresent,
                    outline: false,
                    tagged: false,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo).ConfigureAwait(false);
                break;
            default:
                await HtmlBrowser.SavePagePdfAsync(
                    Url,
                    OutFile,
                    Browser,
                    Clean.IsPresent,
                    Delay,
                    Selector,
                    Landscape.IsPresent,
                    PrintBackground.IsPresent,
                    Format,
                    Width,
                    Height,
                    MarginTop,
                    MarginRight,
                    MarginBottom,
                    MarginLeft,
                    PageRanges,
                    Scale,
                    DisplayHeaderFooter.IsPresent,
                    HeaderTemplate,
                    FooterTemplate,
                    PreferCssPageSize.IsPresent,
                    outline: false,
                    tagged: false,
                    headless: !Visible.IsPresent,
                    slowMo: SlowMo).ConfigureAwait(false);
                break;
        }

        if (Open.IsPresent) {
            Process.Start(new ProcessStartInfo {
                FileName = OutFile,
                UseShellExecute = true,
            });
        }
    }
}
