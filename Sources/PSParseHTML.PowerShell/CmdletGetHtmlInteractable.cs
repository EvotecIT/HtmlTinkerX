using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns interactive elements from an active browser session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLInteractable")]
[OutputType(typeof(HtmlInteractableInfo))]
public sealed class CmdletGetHtmlInteractable : AsyncPSCmdlet {
    /// <summary>Browser session containing the page.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public BrowserSession? Session { get; set; }

    /// <summary>Optional case-insensitive filter applied to the element text.</summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        BrowserSession session = Session ?? (BrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        List<HtmlInteractableInfo> list = await HtmlBrowserRenderer.GetInteractablesAsync(session.Page).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(Filter)) {
            list = list.FindAll(x => x.Text.IndexOf(Filter, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        WriteObject(list, true);
    }
}
