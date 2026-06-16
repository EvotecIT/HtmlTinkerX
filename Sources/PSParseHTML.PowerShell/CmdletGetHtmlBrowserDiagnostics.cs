using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns browser runtime, storage, console, and observed network diagnostics for a session.
/// </summary>
/// <remarks>
/// Use this command to understand extraction reliability signals such as viewport, locale, storage keys,
/// failed requests, console errors, Fetch/XHR calls, and WebSocket activity. It reports diagnostics only;
/// it does not attempt to hide automation or bypass site protections.
/// </remarks>
/// <example>
///   <summary>Inspect an active browser extraction session</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/app -Session
/// Wait-HtmlBrowserContent -Session $session -Stable
/// $diagnostics = Get-HtmlBrowserDiagnostics -Session $session
/// $diagnostics.ObservedApiCalls
/// $diagnostics.ConsistencyWarnings
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserDiagnostics")]
[OutputType(typeof(HtmlBrowserDiagnostics))]
[Alias("Get-HtmlDiagnostics")]
public sealed class CmdletGetHtmlBrowserDiagnostics : AsyncPSCmdlet {
    /// <summary>Browser session to inspect.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserDiagnostics diagnostics = await HtmlBrowser.GetDiagnosticsAsync(session, linkedCts.Token).ConfigureAwait(false);
        WriteObject(diagnostics);
    }
}
