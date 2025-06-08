using System.Management.Automation;
using System.Threading.Tasks;
using System.IO;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
/// <example>
/// <code>Invoke-HTMLRendering -Url https://example.com -Browser Chromium -Clean</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLRendering", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(HtmlRenderResult))]
public sealed class CmdletInvokeHtmlRendering : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional file path to save the rendered HTML.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <summary>Directory to save any downloaded files.</summary>
    [Parameter]
    public string? DownloadPath { get; set; }

    /// <summary>Optional filter applied to download URLs or file names.</summary>
    [Parameter]
    public string? DownloadFilter { get; set; }

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlRenderResult result = await HtmlBrowserRenderer.GetPageContentAsync(
            Url,
            Browser,
            Clean.IsPresent,
            DownloadPath,
            DownloadFilter).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutFile)) {
            File.WriteAllText(OutFile, result.Html);
        }

        WriteObject(result);
    }
}
