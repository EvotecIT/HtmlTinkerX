using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Gets localStorage and sessionStorage entries from an active browser session.
/// </summary>
/// <example>
///   <summary>List storage entries after rendering</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/app -Session
/// Get-HtmlBrowserStorage -Session $session -Scope All
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserStorage")]
[OutputType(typeof(HtmlBrowserStorageItem))]
[Alias("Get-HtmlStorage")]
public sealed class CmdletGetHtmlBrowserStorage : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Storage scope to read.</summary>
    [Parameter]
    [ValidateSet("All", "Local", "Session")]
    public string Scope { get; set; } = "All";

    /// <summary>Optional storage key to read.</summary>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        var items = await HtmlBrowser.GetStorageAsync(session, Scope, Key, linkedCts.Token).ConfigureAwait(false);
        WriteObject(items, true);
    }
}
