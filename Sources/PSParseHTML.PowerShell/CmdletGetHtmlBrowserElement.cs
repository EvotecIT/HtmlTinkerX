using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns browser-observed DOM element information from an active session.
/// </summary>
/// <example>
///   <summary>List visible product cards with attributes and geometry</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/catalog -Session
/// Get-HtmlBrowserElement -Session $session -Selector '.product-card' -VisibleOnly -IncludeAttributes
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserElement")]
[OutputType(typeof(HtmlBrowserElementInfo))]
[Alias("Get-HtmlElement")]
public sealed class CmdletGetHtmlBrowserElement : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector to inspect.</summary>
    [Parameter(Position = 1)]
    public string Selector { get; set; } = "*";

    /// <summary>Return only visible elements.</summary>
    [Parameter]
    public SwitchParameter VisibleOnly { get; set; }

    /// <summary>Include all element attributes.</summary>
    [Parameter]
    public SwitchParameter IncludeAttributes { get; set; }

    /// <summary>Include inner and outer HTML for each element.</summary>
    [Parameter]
    public SwitchParameter IncludeHtml { get; set; }

    /// <summary>Maximum number of matching elements to inspect.</summary>
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
        var elements = await HtmlBrowser.GetElementsAsync(
            session,
            Selector,
            VisibleOnly.IsPresent,
            IncludeAttributes.IsPresent,
            IncludeHtml.IsPresent,
            Limit,
            Timeout,
            linkedCts.Token).ConfigureAwait(false);
        WriteObject(elements, true);
    }
}
