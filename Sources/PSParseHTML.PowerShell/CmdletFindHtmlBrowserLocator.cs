using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Finds ranked locator candidates for resilient browser automation.
/// </summary>
/// <example>
///   <summary>Find locator candidates for a search input</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/search
/// $locator = Find-HtmlBrowserLocator -Session $session -Query Search | Select-Object -First 1
/// $locator | Select-Object Strategy, Selector, SuggestedCommand, TestCommand, Warnings
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlBrowserLocator")]
[OutputType(typeof(HtmlBrowserLocatorCandidate))]
public sealed class CmdletFindHtmlBrowserLocator : AsyncPSCmdlet {
    /// <summary>Existing browser session. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Optional text, label, id, name, placeholder, href, or selector fragment to filter candidates.</summary>
    [Parameter(Position = 1)]
    public string? Query { get; set; }

    /// <summary>Include hidden candidates.</summary>
    [Parameter]
    public SwitchParameter IncludeHidden { get; set; }

    /// <summary>Maximum number of candidates to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 25;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        IReadOnlyList<HtmlBrowserLocatorCandidate> candidates = await HtmlBrowser.FindLocatorCandidatesAsync(
            session,
            Query,
            visibleOnly: !IncludeHidden.IsPresent,
            limit: Limit,
            cancellationToken: linkedCts.Token).ConfigureAwait(false);

        WriteObject(candidates, true);
    }
}
