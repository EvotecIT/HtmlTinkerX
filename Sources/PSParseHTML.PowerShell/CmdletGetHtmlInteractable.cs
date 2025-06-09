using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that lists clickable or interactable elements from a browser session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLInteractable")]
[OutputType(typeof(InteractableElement))]
public sealed class CmdletGetHtmlInteractable : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public BrowserSession Session { get; set; } = null!;

    /// <summary>Include elements hidden from view.</summary>
    [Parameter]
    public SwitchParameter IncludeHidden { get; set; }

    /// <summary>Maximum number of elements to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 100;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        List<InteractableElement> list = await HtmlBrowserRenderer.GetInteractableElementsAsync(
            Session.Page,
            IncludeHidden.IsPresent,
            Limit).ConfigureAwait(false);

        foreach (InteractableElement element in list) {
            WriteObject(element);
        }
    }
}
