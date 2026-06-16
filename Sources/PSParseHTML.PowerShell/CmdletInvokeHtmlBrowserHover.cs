using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Hovers over an element in a browser session before extraction.
/// </summary>
/// <example>
///   <summary>Reveal hover-only content</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/products -Session
/// Invoke-HtmlBrowserHover -Session $session -Selector '.product-card'
/// Wait-HtmlBrowserContent -Session $session -Text 'Quick view' -Selector 'main'
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserHover")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Invoke-HtmlHover")]
public sealed class CmdletInvokeHtmlBrowserHover : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element to hover.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

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
        await HtmlBrowser.HoverAsync(session, Selector, Timeout, linkedCts.Token).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
