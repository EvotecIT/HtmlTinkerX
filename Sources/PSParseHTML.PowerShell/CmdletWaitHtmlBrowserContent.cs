using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Waits for browser-rendered text or DOM stability.
/// </summary>
/// <example>
///   <summary>Wait for rendered search results</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/search -Session
/// Wait-HtmlBrowserContent -Session $session -Text 'Results' -Selector 'main'
///   </code>
/// </example>
/// <example>
///   <summary>Wait until the DOM stops changing</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/app -Session
/// Wait-HtmlBrowserContent -Session $session -Stable -StableMilliseconds 500
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Wait, "HtmlBrowserContent", DefaultParameterSetName = ParameterSetText)]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Wait-HtmlContent")]
public sealed class CmdletWaitHtmlBrowserContent : AsyncPSCmdlet {
    private const string ParameterSetText = "Text";
    private const string ParameterSetStable = "Stable";
    private const string ParameterSetElement = "Element";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Text to wait for.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetText)]
    public string? Text { get; set; }

    /// <summary>Selector scope used when waiting for text.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetElement)]
    public string Selector { get; set; } = "body";

    /// <summary>Use exact text match.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    public SwitchParameter Exact { get; set; }

    /// <summary>Wait until the document HTML is stable.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetStable)]
    public SwitchParameter Stable { get; set; }

    /// <summary>Stable interval in milliseconds.</summary>
    [Parameter(ParameterSetName = ParameterSetStable)]
    [ValidateRange(0, int.MaxValue)]
    public int StableMilliseconds { get; set; } = 500;

    /// <summary>Polling interval in milliseconds for stability checks.</summary>
    [Parameter(ParameterSetName = ParameterSetStable)]
    [ValidateRange(1, int.MaxValue)]
    public int PollMilliseconds { get; set; } = 100;

    /// <summary>Wait for an element state instead of text.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetElement)]
    public SwitchParameter Element { get; set; }

    /// <summary>Wait until the element is visible.</summary>
    [Parameter(ParameterSetName = ParameterSetElement)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Wait until the element is hidden or absent.</summary>
    [Parameter(ParameterSetName = ParameterSetElement)]
    public SwitchParameter Hidden { get; set; }

    /// <summary>Wait until the element is enabled.</summary>
    [Parameter(ParameterSetName = ParameterSetElement)]
    public SwitchParameter Enabled { get; set; }

    /// <summary>Wait until the element is disabled.</summary>
    [Parameter(ParameterSetName = ParameterSetElement)]
    public SwitchParameter Disabled { get; set; }

    /// <summary>Wait until the element is inside the viewport.</summary>
    [Parameter(ParameterSetName = ParameterSetElement)]
    public SwitchParameter InViewport { get; set; }

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

        if (ParameterSetName == ParameterSetStable) {
            await HtmlBrowser.WaitUntilStableAsync(session, StableMilliseconds, PollMilliseconds, Timeout, linkedCts.Token).ConfigureAwait(false);
        } else if (ParameterSetName == ParameterSetElement) {
            if (!Visible.IsPresent && !Hidden.IsPresent && !Enabled.IsPresent && !Disabled.IsPresent && !InViewport.IsPresent) {
                Visible = new SwitchParameter(true);
            }

            await HtmlBrowser.WaitForElementStateAsync(
                session,
                Selector,
                Visible.IsPresent,
                Hidden.IsPresent,
                Enabled.IsPresent,
                Disabled.IsPresent,
                checkedState: false,
                uncheckedState: false,
                selected: false,
                InViewport.IsPresent,
                Timeout,
                PollMilliseconds,
                linkedCts.Token).ConfigureAwait(false);
        } else {
            await HtmlBrowser.WaitForTextAsync(session, Text!, Selector, Exact.IsPresent, Timeout, linkedCts.Token).ConfigureAwait(false);
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
