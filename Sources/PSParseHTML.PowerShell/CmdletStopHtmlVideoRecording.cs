using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;


[Cmdlet(VerbsLifecycle.Stop, "HTMLVideoRecording")]
public sealed class CmdletStopHtmlVideoRecording : AsyncPSCmdlet {
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter]
    public string? OutFile { get; set; }

    protected override async Task ProcessRecordAsync() {
        if (!string.IsNullOrEmpty(OutFile) && System.IO.Path.GetExtension(OutFile) != ".webm") {
            throw new PSArgumentException("Only .webm files are supported.", nameof(OutFile));
        }

        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        await HtmlBrowser.StopVideoRecordingAsync(session, OutFile).ConfigureAwait(false);
        object? defaultSession = GetVariableValue("PSParseHTML_DefaultSession");
        if (defaultSession is HtmlBrowserSession sess && ReferenceEquals(sess, session)) {
            SessionState.PSVariable.Remove("PSParseHTML_DefaultSession");
        }
    }
}
