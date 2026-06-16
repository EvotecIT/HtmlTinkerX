using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Scrolls an element into view in a browser session before extraction.
/// </summary>
/// <example>
///   <summary>Scroll to lazy-loaded content</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/catalog -Session
/// Invoke-HtmlBrowserScroll -Session $session -Selector '.product-card:last-child'
/// Wait-HtmlBrowserContent -Session $session -Text 'Products' -Selector 'main'
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserScroll")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Invoke-HtmlScroll")]
public sealed class CmdletInvokeHtmlBrowserScroll : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element to scroll into view.</summary>
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
        await HtmlBrowser.ScrollIntoViewAsync(session, Selector, Timeout, linkedCts.Token).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
