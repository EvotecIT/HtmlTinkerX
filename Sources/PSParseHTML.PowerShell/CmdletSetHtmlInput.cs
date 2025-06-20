using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that fills text into an input element.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HTMLInput")]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletSetHtmlInput : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the input element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Value to enter.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        await HtmlBrowser.FillInputAsync(session, Selector, Value, Timeout, token).ConfigureAwait(false);

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
