using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves network traffic from a browser session to a HAR file.
/// </summary>
[Cmdlet(VerbsData.Save, "HTMLHar")]
public sealed class CmdletSaveHtmlHar : AsyncPSCmdlet {
    /// <summary>
    /// Browser session to export.
    /// </summary>
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

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
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.ExportHarAsync(session, OutFile, token).ConfigureAwait(false);
    }
}