using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that disposes an existing browser session.
/// </summary>
[Cmdlet(VerbsCommon.Close, "HTMLSession")]
[Alias("Stop-HTMLSession")]
public sealed class CmdletCloseHtmlSession : AsyncPSCmdlet {
    /// <summary>Browser session to dispose.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession Session { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        await HtmlBrowser.CloseSessionAsync(Session).ConfigureAwait(false);
        object? defaultSession = GetVariableValue("PSParseHTML_DefaultSession");
        if (defaultSession is HtmlBrowserSession sess && ReferenceEquals(sess, Session)) {
            SessionState.PSVariable.Remove("PSParseHTML_DefaultSession");
        }
    }
}

