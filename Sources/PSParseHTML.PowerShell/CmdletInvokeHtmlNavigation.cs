using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that navigates an existing browser session to a new URL.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLNavigation")]
[OutputType(typeof(BrowserSession))]
public sealed class CmdletInvokeHtmlNavigation : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public BrowserSession Session { get; set; } = null!;

    /// <summary>Destination URL.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Url { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        await Session.Page.GotoAsync(Url).ConfigureAwait(false);
        await Session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        WriteObject(Session);
    }
}

