using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsLifecycle.Stop, "HTMLVideoRecording")]
public sealed class CmdletStopHtmlVideoRecording : AsyncPSCmdlet {
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession Session { get; set; } = null!;

    [Parameter]
    public string? OutFile { get; set; }

    protected override async Task ProcessRecordAsync() {
        await HtmlBrowser.StopVideoRecordingAsync(Session, OutFile).ConfigureAwait(false);
        object? defaultSession = GetVariableValue("PSParseHTML_DefaultSession");
        if (defaultSession is HtmlBrowserSession sess && ReferenceEquals(sess, Session)) {
            SessionState.PSVariable.Remove("PSParseHTML_DefaultSession");
        }
    }
}
