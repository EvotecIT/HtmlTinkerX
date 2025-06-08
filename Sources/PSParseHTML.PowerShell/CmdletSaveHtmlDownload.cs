using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that saves files downloaded while rendering a web page.
/// </summary>
/// <example>
///   <code>Save-HTMLDownload -Url https://example.com/download.html -Path C:\temp</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLDownload", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(string[]))]
public sealed class CmdletSaveHtmlDownload : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Directory where downloads will be saved.</summary>
    [Parameter(Mandatory = true)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <summary>Optional filter applied to download URLs or file names.</summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        List<string> files = await HtmlBrowserRenderer.SavePageDownloadsAsync(
            Url,
            Path,
            Browser,
            Clean.IsPresent,
            Filter).ConfigureAwait(false);

        WriteObject(files.ToArray(), true);
    }
}
