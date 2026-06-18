using HtmlTinkerX;
using Microsoft.Playwright;
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
/// $session = Start-HtmlBrowserSession -Url https://example.org/products
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

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context if hover fails.</summary>
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
            await HtmlBrowser.HoverAsync(session, Selector, Timeout, token).ConfigureAwait(false);
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
            await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "Hover", ex, FailureEvidenceFolder, token).ConfigureAwait(false);
            throw;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
