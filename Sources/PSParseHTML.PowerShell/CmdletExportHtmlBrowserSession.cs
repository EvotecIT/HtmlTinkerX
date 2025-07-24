using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that saves cookies and storage state of a browser session to disk.
/// </summary>
/// <example>
/// <code>Export-HTMLSession -Session $session -Path session.json</code>
/// </example>
[Cmdlet(VerbsData.Export, "HtmlBrowserSession")]
[Alias("Export-HTMLSession")]
public sealed class CmdletExportHtmlBrowserSession : AsyncPSCmdlet {
    /// <summary>Browser session to export.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Path for the exported session state file.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        Session ??= (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.ExportSessionAsync(Session, Path, token).ConfigureAwait(false);
    }
}