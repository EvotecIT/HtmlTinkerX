using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that stops Playwright tracing for a browser session.
/// </summary>
[Cmdlet(VerbsLifecycle.Stop, "HtmlBrowserTracing")]
[Alias("Stop-HtmlTracing")]
public sealed class CmdletStopHtmlBrowserTracing : AsyncPSCmdlet {
    /// <summary>Browser session to stop tracing for.</summary>
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Output file where the trace will be saved.</summary>
    [Parameter(Mandatory = true)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Optional cancellation token for the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.StopTracingAsync(session, OutFile, token).ConfigureAwait(false);
    }
}
