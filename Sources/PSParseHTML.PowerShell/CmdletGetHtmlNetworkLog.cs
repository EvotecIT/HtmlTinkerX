using System.Collections.Generic;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves captured network log entries from a session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLNetworkLog")]
[OutputType(typeof(HtmlNetworkEntry))]
public sealed class CmdletGetHtmlNetworkLog : PSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        WriteObject(HtmlBrowser.GetNetworkLog(session), true);
    }
}
