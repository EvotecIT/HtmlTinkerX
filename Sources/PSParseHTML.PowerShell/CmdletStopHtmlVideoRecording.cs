using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that stops video recording for a browser session.
/// </summary>
[Cmdlet(VerbsLifecycle.Stop, "HTMLVideoRecording")]
public sealed class CmdletStopHtmlVideoRecording : AsyncPSCmdlet {
    /// <summary>Browser session with an active recording.</summary>
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Optional path to save the recorded video.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!string.IsNullOrEmpty(OutFile) && !OutFile.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) {
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
