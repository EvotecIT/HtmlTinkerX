using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that adds cookies to an active browser session.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HTMLCookie")]
public sealed class CmdletSetHtmlCookie : AsyncPSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Cookies to add.</summary>
    [Parameter(Mandatory = true)]
    public HtmlCookie[]? Cookie { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        IEnumerable<HtmlCookie> cookies = Cookie ?? System.Array.Empty<HtmlCookie>();
        await HtmlBrowser.SetCookiesAsync(session, cookies).ConfigureAwait(false);
    }
}
