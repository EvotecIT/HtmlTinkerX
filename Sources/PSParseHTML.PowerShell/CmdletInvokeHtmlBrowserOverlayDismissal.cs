using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Attempts to dismiss common cookie and modal overlays.
/// </summary>
/// <remarks>
/// This helper tries common visible buttons and selectors such as Accept, I agree, Got it, and Close.
/// It is intended to remove extraction-blocking overlays in legitimate workflows, not to bypass access controls.
/// </remarks>
/// <example>
///   <summary>Dismiss a cookie banner before reading content</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/article
/// Close-HtmlBrowserOverlay -Session $session
/// Get-HtmlBrowserContent -Session $session -Selector 'main' -AsText
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Close, "HtmlBrowserOverlay")]
[OutputType(typeof(string))]
[Alias("Invoke-HtmlBrowserOverlayDismissal", "Invoke-HtmlOverlayDismissal", "Close-HtmlOverlay")]
public sealed class CmdletInvokeHtmlBrowserOverlayDismissal : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Timeout in milliseconds for each dismissal target.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 1500;

    /// <summary>Delay after each successful dismissal.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int InteractionDelayMs { get; set; } = 150;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        IReadOnlyList<string> applied = await HtmlBrowser.DismissCommonOverlaysAsync(session, Timeout, InteractionDelayMs, linkedCts.Token).ConfigureAwait(false);
        WriteObject(applied, true);
    }
}
