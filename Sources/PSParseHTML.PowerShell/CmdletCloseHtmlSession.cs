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
    public BrowserSession Session { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        await HtmlBrowserRenderer.CloseSessionAsync(Session).ConfigureAwait(false);
    }
}

