using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves network traffic from a browser session to a HAR file.
/// </summary>
[Cmdlet(VerbsData.Save, "HTMLHar")]
public sealed class CmdletSaveHtmlHar : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetHar = "Har";
    /// <summary>
    /// Browser session to export.
    /// </summary>
    [Parameter(ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>
    /// HAR object to write.
    /// </summary>
    [Parameter(ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetHar)]
    public Har? Har { get; set; }

    /// <summary>
    /// Destination HAR file path.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>
    /// Optional cancellation token.
    /// </summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        if (ParameterSetName == ParameterSetHar) {
#if NETSTANDARD2_0 || FRAMEWORK
            using FileStream fs = File.Create(OutFile);
            await HtmlHarViewer.WriteHarAsync(Har ?? throw new PSArgumentNullException(nameof(Har)), fs).ConfigureAwait(false);
#else
            await using FileStream fs = File.Create(OutFile);
            await HtmlHarViewer.WriteHarAsync(Har ?? throw new PSArgumentNullException(nameof(Har)), fs).ConfigureAwait(false);
#endif
        } else {
            HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                ?? throw new PSInvalidOperationException("No session provided and no default session found.");
            await HtmlBrowser.ExportHarAsync(session, OutFile, token).ConfigureAwait(false);
        }
    }
}