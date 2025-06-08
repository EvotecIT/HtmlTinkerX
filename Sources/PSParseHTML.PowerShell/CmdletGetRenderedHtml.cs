using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
[Cmdlet(VerbsCommon.Get, "RenderedHtml", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(string))]
public sealed class CmdletGetRenderedHtml : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional file path to save the rendered HTML.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!string.IsNullOrEmpty(OutFile)) {
            await HtmlBrowserRenderer.SavePageContentAsync(Url, OutFile).ConfigureAwait(false);
        } else {
            string html = await HtmlBrowserRenderer.GetPageContentAsync(Url).ConfigureAwait(false);
            WriteObject(html);
        }
    }
}
