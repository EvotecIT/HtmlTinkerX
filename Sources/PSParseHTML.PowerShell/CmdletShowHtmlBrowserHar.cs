using HtmlTinkerX;
using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Generates a simple HTML viewer for a HAR file and returns the parsed data.
/// </summary>
[Cmdlet(VerbsCommon.Show, "HtmlBrowserHar")]
[Alias("Show-HtmlHar")]
public sealed class CmdletShowHtmlBrowserHar : AsyncPSCmdlet {
    /// <summary>Path to the HAR file.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional output HTML file path.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <summary>Open the generated HTML viewer.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        Har har = await HtmlHarViewer.ReadHarAsync(Path).ConfigureAwait(false);
        string resolved = Path.ToFullPath();
        string outPath = OutFile is null
            ? System.IO.Path.ChangeExtension(resolved, ".html")
            : OutFile.ToFullPath();

        string html = HtmlHarViewer.BuildViewerHtml(har);
#if NETSTANDARD2_0 || NETFRAMEWORK
        System.IO.File.WriteAllText(outPath, html);
#else
        await System.IO.File.WriteAllTextAsync(outPath, html, CancelToken).ConfigureAwait(false);
#endif
        if (Open.IsPresent) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = outPath,
                    UseShellExecute = true,
                });
            } catch (System.Exception ex) {
                WriteVerbose($"Failed to open file '{outPath}': {ex.Message}");
            }
        }

        WriteObject(har);
    }
}
