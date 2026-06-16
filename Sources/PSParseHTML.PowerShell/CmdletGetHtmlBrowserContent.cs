using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML or text content from an existing session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserContent")]
[OutputType(typeof(string))]
[Alias("Get-HtmlContent")]
public sealed class CmdletGetHtmlBrowserContent : AsyncPSCmdlet {
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

    /// <summary>Return content for all matching elements instead of only the first match.</summary>
    [Parameter]
    public SwitchParameter All { get; set; }

    /// <summary>Maximum number of matching elements to return when using <see cref="All"/>.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 100;

    /// <summary>Timeout in milliseconds while waiting for the selector.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

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

        if (All.IsPresent) {
            if (string.IsNullOrWhiteSpace(Selector)) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException("-All requires -Selector."),
                    "MissingSelector",
                    ErrorCategory.InvalidArgument,
                    Selector));
                return;
            }

            var elements = await HtmlBrowser.GetElementsAsync(session, Selector!, visibleOnly: false, includeAttributes: false, includeHtml: true, limit: Limit, timeout: Timeout, cancellationToken: token).ConfigureAwait(false);
            string[] values = elements
                .Select(element => AsText.IsPresent ? element.Text : InnerHtml.IsPresent ? element.InnerHtml ?? string.Empty : element.OuterHtml ?? string.Empty)
                .ToArray();
            WriteObject(values, true);
            return;
        }

        string result = await HtmlBrowser.GetContentAsync(
            session.Page,
            Selector,
            InnerHtml.IsPresent,
            AsText.IsPresent,
            Timeout,
            token).ConfigureAwait(false);

        WriteObject(result);
    }
}
