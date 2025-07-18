using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML or text content from an existing session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLContent")]
[OutputType(typeof(string))]
public sealed class CmdletGetHtmlContent : AsyncPSCmdlet {
    /// <summary>Browser session in use.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

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

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        int flags = (InnerHtml.IsPresent ? 1 : 0) + (OuterHtml.IsPresent ? 1 : 0) + (AsText.IsPresent ? 1 : 0);
        if (flags > 1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException("Specify only one of -InnerHtml, -OuterHtml, or -AsText."),
                "InvalidParameter", ErrorCategory.InvalidArgument, Selector));
            return;
        }

        string result = await HtmlBrowser.GetContentAsync(
            session.Page,
            Selector,
            InnerHtml.IsPresent,
            AsText.IsPresent,
            token).ConfigureAwait(false);

        WriteObject(result);
    }
}

