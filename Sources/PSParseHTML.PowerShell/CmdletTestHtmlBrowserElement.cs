using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Tests browser-observed state for an element in an active session.
/// </summary>
/// <example>
///   <summary>Check whether results are visible</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/search
/// Test-HtmlBrowserElement -Session $session -Selector '#results' -Visible
///   </code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "HtmlBrowserElement")]
[OutputType(typeof(bool))]
[Alias("Test-HtmlElement")]
public sealed class CmdletTestHtmlBrowserElement : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector to test.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Require the element to be visible.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Require the element to be hidden or absent.</summary>
    [Parameter]
    public SwitchParameter Hidden { get; set; }

    /// <summary>Require the element to be enabled.</summary>
    [Parameter]
    public SwitchParameter Enabled { get; set; }

    /// <summary>Require the element to be disabled.</summary>
    [Parameter]
    public SwitchParameter Disabled { get; set; }

    /// <summary>Require the element to be checked.</summary>
    [Parameter]
    public SwitchParameter Checked { get; set; }

    /// <summary>Require the element to be unchecked.</summary>
    [Parameter]
    public SwitchParameter Unchecked { get; set; }

    /// <summary>Require the element to be selected.</summary>
    [Parameter]
    public SwitchParameter Selected { get; set; }

    /// <summary>Require the element to intersect the viewport.</summary>
    [Parameter]
    public SwitchParameter InViewport { get; set; }

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
        bool result = await HtmlBrowser.TestElementAsync(
            session,
            Selector,
            Visible.IsPresent,
            Hidden.IsPresent,
            Enabled.IsPresent,
            Disabled.IsPresent,
            Checked.IsPresent,
            Unchecked.IsPresent,
            Selected.IsPresent,
            InViewport.IsPresent,
            Timeout,
            linkedCts.Token).ConfigureAwait(false);
        WriteObject(result);
    }
}
