using System;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that registers a Playwright route handler for an active session.
/// </summary>
[Cmdlet(VerbsLifecycle.Register, "HTMLRoute")]
public sealed class CmdletRegisterHtmlRoute : AsyncPSCmdlet {
    /// <summary>Browser session in use.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>URL pattern for the route.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Script block executed for each matching request.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public ScriptBlock? ScriptBlock { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        ScriptBlock block = ScriptBlock ?? throw new PSArgumentNullException(nameof(ScriptBlock));
        Func<IRoute, Task> handler = route => {
            object? result = block.InvokeReturnAsIs(route);
            return result is Task t ? t : Task.CompletedTask;
        };

        await HtmlBrowser.RegisterRouteAsync(session, Pattern, handler, token).ConfigureAwait(false);
        WriteObject(handler);
    }
}
