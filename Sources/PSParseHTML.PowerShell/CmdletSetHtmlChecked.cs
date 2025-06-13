using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that sets the checked state of a checkbox or radio button.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HTMLChecked")]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletSetHtmlChecked : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Uncheck the element instead of checking it.</summary>
    [Parameter]
    public SwitchParameter Uncheck { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        await HtmlBrowser.SetCheckedAsync(session, Selector, !Uncheck.IsPresent, Timeout).ConfigureAwait(false);

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
