using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that removes a previously registered Playwright route handler.
/// </summary>
[Cmdlet(VerbsLifecycle.Unregister, "HTMLRoute")]
public sealed class CmdletUnregisterHtmlRoute : AsyncPSCmdlet {
    /// <summary>Browser session in use.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL pattern for the route.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Handler returned by Register-HTMLRoute.</summary>
    [Parameter(Position = 2)]
    public Delegate? Handler { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        await HtmlBrowser.UnregisterRouteAsync(session, Pattern, Handler as Func<IRoute, Task>, token).ConfigureAwait(false);
    }
}
