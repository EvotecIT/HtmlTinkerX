using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>Cookie domain filter.</summary>
    [Parameter]
    public string[]? Domain { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        List<HtmlCookie> cookies = await HtmlBrowser.GetCookiesAsync(session, token).ConfigureAwait(false);
        if (Domain is { Length: > 0 }) {
            var domainSet = new HashSet<string>(Domain, System.StringComparer.OrdinalIgnoreCase);
            cookies = cookies.Where(c => c.Domain is not null && domainSet.Contains(c.Domain)).ToList();
        }
        WriteObject(cookies, true);
    }
}
