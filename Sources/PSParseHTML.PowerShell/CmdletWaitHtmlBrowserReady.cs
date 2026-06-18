using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Waits for browser readiness conditions before continuing an automation workflow.
/// </summary>
/// <example>
///   <summary>Wait for an app shell to become usable</summary>
///   <code>Wait-HtmlBrowserReady -Session $session -LoadState DomContentLoaded -Selector main -Function '() => window.appReady === true' -Stable</code>
/// </example>
[Cmdlet(VerbsLifecycle.Wait, "HtmlBrowserReady")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Wait-HtmlReady")]
public sealed class CmdletWaitHtmlBrowserReady : AsyncPSCmdlet {
    /// <summary>Existing browser session. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Load state to wait for before other readiness checks.</summary>
    [Parameter]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Skip the load-state wait and only use selector, function, or stability checks.</summary>
    [Parameter]
    public SwitchParameter NoLoadState { get; set; }

    /// <summary>Selector that must exist before readiness completes.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>JavaScript predicate that must evaluate truthy before readiness completes.</summary>
    [Parameter]
    [Alias("WaitForFunction")]
    public string? Function { get; set; }

    /// <summary>Wait until the document HTML is stable.</summary>
    [Parameter]
    public SwitchParameter Stable { get; set; }

    /// <summary>Stable interval in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int StableMilliseconds { get; set; } = 500;

    /// <summary>Polling interval in milliseconds for stability checks.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int PollMilliseconds { get; set; } = 100;

    /// <summary>Timeout in milliseconds for each readiness condition.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object after readiness completes.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, and failure context if readiness wait fails.</summary>
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

        HtmlBrowserReadinessOptions options = new() {
            LoadState = MyInvocation.BoundParameters.ContainsKey(nameof(LoadState)) ? LoadState : HtmlBrowserLoadState.NetworkIdle,
            SkipLoadState = NoLoadState.IsPresent,
            Selector = Selector,
            Function = Function,
            Stable = Stable.IsPresent,
            StableMilliseconds = StableMilliseconds,
            PollMilliseconds = PollMilliseconds,
            Timeout = Timeout
        };

        HtmlBrowserSession readySession;
        try {
            readySession = await HtmlBrowser.WaitUntilReadyAsync(session, options, linkedCts.Token).ConfigureAwait(false);
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
            await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "ReadyWait", ex, FailureEvidenceFolder, linkedCts.Token).ConfigureAwait(false);
            throw;
        }

        if (PassThru.IsPresent) {
            WriteObject(readySession);
        }
    }
}
