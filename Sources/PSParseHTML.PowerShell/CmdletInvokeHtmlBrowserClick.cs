using HtmlTinkerX;
using Microsoft.Playwright;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Clicks a selector or visible text target in a browser session before extraction.
/// </summary>
/// <example>
///   <summary>Click a selector and keep the session alive</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/app
/// Invoke-HtmlBrowserClick -Session $session -Selector '#loadMore'
/// Wait-HtmlBrowserContent -Session $session -Text 'More results' -Selector 'main'
///   </code>
/// </example>
/// <example>
///   <summary>Click by visible text only when the target is present</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/app
/// Invoke-HtmlBrowserClick -Session $session -Text 'Accept' -IfVisible
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserClick", DefaultParameterSetName = ParameterSetSelector)]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Invoke-HtmlClick")]
public sealed class CmdletInvokeHtmlBrowserClick : AsyncPSCmdlet {
    private const string ParameterSetSelector = "BySelector";
    private const string ParameterSetText = "ByText";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element to click.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetSelector)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Visible text of the element to click.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetText)]
    public string? Text { get; set; }

    /// <summary>Use exact text match.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    public SwitchParameter Exact { get; set; }

    /// <summary>Regular expression for text match.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    public string? Regex { get; set; }

    /// <summary>Mouse button to use.</summary>
    [Parameter(ParameterSetName = ParameterSetSelector)]
    public MouseButton Button { get; set; } = MouseButton.Left;

    /// <summary>Number of clicks.</summary>
    [Parameter(ParameterSetName = ParameterSetSelector)]
    public int ClickCount { get; set; } = 1;

    /// <summary>Zero-based index of the matching selector or text target to click.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int? Nth { get; set; }

    /// <summary>Keyboard modifiers.</summary>
    [Parameter(ParameterSetName = ParameterSetSelector)]
    public KeyboardModifier[]? Modifier { get; set; }

    /// <summary>Return without error when the target is absent, hidden, or times out.</summary>
    [Parameter]
    public SwitchParameter IfVisible { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, and failure context if the click fails.</summary>
    [Parameter]
    public SwitchParameter OnFailureEvidence { get; set; }

    /// <summary>Root folder where failure evidence is written when <see cref="OnFailureEvidence"/> is used.</summary>
    [Parameter]
    public string? FailureEvidenceFolder { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ClickCount < 1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentOutOfRangeException(nameof(ClickCount), ClickCount, "ClickCount must be positive."),
                "ClickCountOutOfRange",
                ErrorCategory.InvalidArgument,
                ClickCount));
            return;
        }
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        try {
            if (ParameterSetName == ParameterSetText) {
                if (IfVisible.IsPresent) {
                    _ = await HtmlBrowser.TryClickTextAsync(session, Text!, Exact.IsPresent, Regex, Timeout, cancellationToken: default, nth: Nth).ConfigureAwait(false);
                } else {
                    await HtmlBrowser.ClickTextAsync(session, Text!, Exact.IsPresent, Regex, waitForNavigation: false, timeout: Timeout, cancellationToken: default, nth: Nth).ConfigureAwait(false);
                }
            } else if (IfVisible.IsPresent) {
                if (Nth.HasValue) {
                    ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("-Nth with -IfVisible selector clicks is not supported. Use text clicks or omit -IfVisible."),
                        "NthIfVisibleSelectorClickOptionConflict",
                        ErrorCategory.InvalidArgument,
                        Selector));
                    return;
                }

                _ = await HtmlBrowser.TryMouseClickAsync(session, Selector, Button, ClickCount, Modifier, Timeout).ConfigureAwait(false);
            } else {
                if (Nth.HasValue && (ClickCount != 1 || Modifier is { Length: > 0 } || Button != MouseButton.Left)) {
                    ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("-Nth with selector clicks supports the default left click only."),
                        "NthClickOptionConflict",
                        ErrorCategory.InvalidArgument,
                        Selector));
                    return;
                }

                if (Nth.HasValue) {
                    await HtmlBrowser.ClickSelectorAsync(session, Selector, waitForNavigation: false, timeout: Timeout, cancellationToken: default, nth: Nth).ConfigureAwait(false);
                } else {
                    await HtmlBrowser.MouseClickAsync(session, Selector, Button, ClickCount, Modifier, Timeout).ConfigureAwait(false);
                }
            }
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
            await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "Click", ex, FailureEvidenceFolder, CancelToken).ConfigureAwait(false);
            throw;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
