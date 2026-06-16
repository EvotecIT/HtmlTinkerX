using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns browser-observed information about the active focused element.
/// </summary>
/// <example>
///   <summary>Inspect the element that receives keyboard input</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/search -Session
/// Invoke-HtmlBrowserClick -Session $session -Selector 'input[type=search]'
/// Get-HtmlBrowserActiveElement -Session $session -IncludeAttributes
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlBrowserActiveElement")]
[OutputType(typeof(HtmlBrowserElementInfo))]
[Alias("Get-HtmlActiveElement")]
public sealed class CmdletGetHtmlBrowserActiveElement : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Include all element attributes.</summary>
    [Parameter]
    public SwitchParameter IncludeAttributes { get; set; }

    /// <summary>Include inner and outer HTML.</summary>
    [Parameter]
    public SwitchParameter IncludeHtml { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserElementInfo? element = await HtmlBrowser.GetActiveElementAsync(
            session,
            IncludeAttributes.IsPresent,
            IncludeHtml.IsPresent,
            linkedCts.Token).ConfigureAwait(false);
        if (element != null) {
            WriteObject(element);
        }
    }
}
