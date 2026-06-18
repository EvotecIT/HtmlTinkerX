using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Imports a reusable browser profile from JSON.
/// </summary>
[Cmdlet(VerbsData.Import, "HtmlBrowserProfile")]
[OutputType(typeof(HtmlBrowserProfile))]
public sealed class CmdletImportHtmlBrowserProfile : AsyncPSCmdlet {
    /// <summary>Path to the browser profile JSON file.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserProfile profile = await HtmlBrowserProfile.LoadAsync(Path, linkedCts.Token).ConfigureAwait(false);
        WriteObject(profile);
    }
}
