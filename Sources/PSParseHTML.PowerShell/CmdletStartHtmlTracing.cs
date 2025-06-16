using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsLifecycle.Start, "HTMLTracing")]
public sealed class CmdletStartHtmlTracing : AsyncPSCmdlet {
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter]
    public SwitchParameter Screenshots { get; set; } = true;

    [Parameter]
    public SwitchParameter Snapshots { get; set; } = true;

    [Parameter]
    public SwitchParameter Sources { get; set; } = true;

    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        await HtmlBrowser.StartTracingAsync(session, Screenshots.IsPresent, Snapshots.IsPresent, Sources.IsPresent).ConfigureAwait(false);
    }
}
