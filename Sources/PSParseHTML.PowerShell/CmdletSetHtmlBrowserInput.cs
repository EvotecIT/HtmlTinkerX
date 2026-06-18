using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Fills or types text into an input element in a browser session.
/// </summary>
/// <example>
///   <summary>Set a search box value in one operation</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/search
/// Set-HtmlBrowserInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX'
///   </code>
/// </example>
/// <example>
///   <summary>Type through keyboard events for reactive inputs</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/search
/// Set-HtmlBrowserInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX' -Type -DelayMs 25
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HtmlBrowserInput")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Set-HtmlInput")]
public sealed class CmdletSetHtmlBrowserInput : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the input element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Value to enter.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Send text through keyboard events instead of replacing the value in one operation.</summary>
    [Parameter]
    public SwitchParameter Type { get; set; }

    /// <summary>Delay in milliseconds between characters when using <see cref="Type"/>.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int DelayMs { get; set; } = 40;

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, and failure context if input fails.</summary>
    [Parameter]
    public SwitchParameter OnFailureEvidence { get; set; }

    /// <summary>Root folder where failure evidence is written when <see cref="OnFailureEvidence"/> is used.</summary>
    [Parameter]
    public string? FailureEvidenceFolder { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        try {
            if (Type.IsPresent) {
                await HtmlBrowser.TypeInputAsync(session, Selector, Value, DelayMs, Timeout, token).ConfigureAwait(false);
            } else {
                await HtmlBrowser.FillInputAsync(session, Selector, Value, Timeout, token).ConfigureAwait(false);
            }
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
            await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "Input", ex, FailureEvidenceFolder, token).ConfigureAwait(false);
            throw;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
