using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves captured console log entries from a session.
/// </summary>
/// <example>
/// <code>Get-HTMLConsoleLog -Session $session</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserConsoleLog")]
[OutputType(typeof(HtmlConsoleEntry))]
[Alias("Get-HTMLConsoleLog")]
public sealed class CmdletGetHtmlBrowserConsoleLog : AsyncPSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Optional severity filter.</summary>
    [Parameter(Position = 1)]
    public HtmlConsoleSeverity? Severity { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        WriteObject(HtmlBrowser.GetConsoleLog(session, Severity), true);
        return Task.CompletedTask;
    }
}