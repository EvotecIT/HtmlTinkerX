using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML or text content from an existing session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLContent")]
[OutputType(typeof(string))]
public sealed class CmdletGetHtmlContent : AsyncPSCmdlet {
    /// <summary>Browser session in use.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public BrowserSession? Session { get; set; }

    /// <summary>CSS selector for the target element.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Return inner HTML instead of outer HTML.</summary>
    [Parameter]
    public SwitchParameter InnerHtml { get; set; }

    /// <summary>Return outer HTML. This is the default.</summary>
    [Parameter]
    public SwitchParameter OuterHtml { get; set; }

    /// <summary>Return text content instead of HTML.</summary>
    [Parameter]
    public SwitchParameter AsText { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        BrowserSession session = Session ?? (BrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        int flags = (InnerHtml.IsPresent ? 1 : 0) + (OuterHtml.IsPresent ? 1 : 0) + (AsText.IsPresent ? 1 : 0);
        if (flags > 1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException("Specify only one of -InnerHtml, -OuterHtml, or -AsText."),
                "InvalidParameter", ErrorCategory.InvalidArgument, Selector));
            return;
        }

        string result = await HtmlBrowserRenderer.GetContentAsync(
            session.Page,
            Selector,
            InnerHtml.IsPresent,
            AsText.IsPresent).ConfigureAwait(false);

        WriteObject(result);
    }
}

