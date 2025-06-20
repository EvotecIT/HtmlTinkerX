using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsLifecycle.Stop, "HTMLTracing")]
public sealed class CmdletStopHtmlTracing : AsyncPSCmdlet {
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter(Mandatory = true)]
    public string OutFile { get; set; } = string.Empty;

    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.StopTracingAsync(session, OutFile, token).ConfigureAwait(false);
    }
}
