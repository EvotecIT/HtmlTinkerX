using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves cookies from an active browser session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLCookie")]
[OutputType(typeof(HtmlCookie))]
public sealed class CmdletGetHtmlCookie : AsyncPSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        List<HtmlCookie> cookies = await HtmlBrowser.GetCookiesAsync(session, token).ConfigureAwait(false);
        WriteObject(cookies, true);
    }
}
