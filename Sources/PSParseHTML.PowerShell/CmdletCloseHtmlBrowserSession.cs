using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that disposes an existing browser session.
/// </summary>
/// <example>
/// <code>Close-HTMLSession -Session $session</code>
/// </example>
[Cmdlet(VerbsCommon.Close, "HtmlBrowserSession")]
[Alias("Close-HtmlSession", "Stop-HtmlSession")]
public sealed class CmdletCloseHtmlBrowserSession : AsyncPSCmdlet {
    /// <summary>Browser session to dispose.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserSession Session { get; set; } = null!;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        await HtmlBrowser.CloseSessionAsync(Session, token).ConfigureAwait(false);
        object? defaultSession = GetVariableValue("PSParseHTML_DefaultSession");
        if (defaultSession is HtmlBrowserSession sess && ReferenceEquals(sess, Session)) {
            SessionState.PSVariable.Remove("PSParseHTML_DefaultSession");
        }
    }
}
