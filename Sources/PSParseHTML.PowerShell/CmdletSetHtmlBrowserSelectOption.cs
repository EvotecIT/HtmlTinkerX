using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that selects options from a &lt;select&gt; element.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HtmlBrowserSelectOption")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Set-HtmlSelectOption")]
public sealed class CmdletSetHtmlBrowserSelectOption : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the &lt;select&gt; element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Option values to select.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public string[] Value { get; set; } = System.Array.Empty<string>();

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        await HtmlBrowser.SelectOptionAsync(session, Selector, Value, Timeout, token).ConfigureAwait(false);

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
