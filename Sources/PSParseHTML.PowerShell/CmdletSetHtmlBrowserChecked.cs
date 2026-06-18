using HtmlTinkerX;
using Microsoft.Playwright;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that sets the checked state of a checkbox or radio button.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HtmlBrowserChecked")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Set-HtmlChecked")]
public sealed class CmdletSetHtmlBrowserChecked : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Uncheck the element instead of checking it.</summary>
    [Parameter]
    public SwitchParameter Uncheck { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context if setting checked state fails.</summary>
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
            await HtmlBrowser.SetCheckedAsync(session, Selector, !Uncheck.IsPresent, Timeout, token).ConfigureAwait(false);
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
            await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "Checked", ex, FailureEvidenceFolder, token).ConfigureAwait(false);
            throw;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
