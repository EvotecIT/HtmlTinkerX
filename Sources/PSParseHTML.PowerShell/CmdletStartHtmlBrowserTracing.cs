using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Starts capturing a Playwright trace for the given session.
/// </summary>
[Cmdlet(VerbsLifecycle.Start, "HtmlBrowserTracing")]
[Alias("Start-HTMLTracing")]
public sealed class CmdletStartHtmlBrowserTracing : AsyncPSCmdlet {
    /// <summary>
    /// Browser session to trace.
    /// </summary>
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Include screenshots in the trace.</summary>
    [Parameter]
    public SwitchParameter Screenshots { get; set; } = true;

    /// <summary>Include DOM snapshots in the trace.</summary>
    [Parameter]
    public SwitchParameter Snapshots { get; set; } = true;

    /// <summary>Include page sources in the trace.</summary>
    [Parameter]
    public SwitchParameter Sources { get; set; } = true;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.StartTracingAsync(session, Screenshots.IsPresent, Snapshots.IsPresent, Sources.IsPresent, token).ConfigureAwait(false);
    }
}