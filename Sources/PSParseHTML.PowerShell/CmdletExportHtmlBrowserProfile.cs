using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Exports a reusable browser profile to JSON.
/// </summary>
[Cmdlet(VerbsData.Export, "HtmlBrowserProfile")]
public sealed class CmdletExportHtmlBrowserProfile : AsyncPSCmdlet {
    /// <summary>Browser profile to export.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserProfile Profile { get; set; } = null!;

    /// <summary>Path to the browser profile JSON file.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        await Profile.SaveAsync(Path, linkedCts.Token).ConfigureAwait(false);
    }
}
