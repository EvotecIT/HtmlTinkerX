using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that adds cookies to an active browser session.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HtmlBrowserCookie")]
[Alias("Set-HtmlCookie")]
public sealed class CmdletSetHtmlBrowserCookie : AsyncPSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Cookies to add.</summary>
    [Parameter(Mandatory = true)]
    [AllowEmptyCollection]
    public HtmlCookie[]? Cookie { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        IEnumerable<HtmlCookie> cookies = Cookie ?? System.Array.Empty<HtmlCookie>();
        await HtmlBrowser.SetCookiesAsync(session, cookies, token).ConfigureAwait(false);
    }
}
