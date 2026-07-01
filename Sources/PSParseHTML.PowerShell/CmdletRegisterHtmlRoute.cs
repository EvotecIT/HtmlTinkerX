using HtmlTinkerX;
using Microsoft.Playwright;
using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that registers a Playwright route handler for an active session.
/// </summary>
[Cmdlet(VerbsLifecycle.Register, "HtmlRoute")]
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
        bool usePowerShellRoute = ExpectsPowerShellRoute(block);
        Runspace? runspace = Runspace.DefaultRunspace;
        object syncRoot = new();
        Func<IRoute, Task> handler = async route => {
            PowerShellHtmlRoute psRoute = new(route);
            object routeArgument = usePowerShellRoute ? psRoute : route;
            object? result;
            lock (syncRoot) {
                Runspace? previousRunspace = Runspace.DefaultRunspace;
                try {
                    if (runspace is not null) {
                        Runspace.DefaultRunspace = runspace;
                    }

                    result = block.InvokeReturnAsIs(routeArgument);
                } finally {
                    Runspace.DefaultRunspace = previousRunspace;
                }
            }

            if (result is PSObject psObject) {
                result = psObject.BaseObject;
            }

            if (result is Task task) {
                await task.ConfigureAwait(false);
            }

            if (usePowerShellRoute) {
                await psRoute.ExecuteAsync().ConfigureAwait(false);
            }
        };

        await HtmlBrowser.RegisterRouteAsync(session, Pattern, handler, token).ConfigureAwait(false);
        WriteObject(handler);
    }

    private static bool ExpectsPowerShellRoute(ScriptBlock block) {
        if (block.Ast is not ScriptBlockAst scriptBlockAst) {
            return false;
        }

        var parameters = scriptBlockAst.ParamBlock?.Parameters;
        if (parameters is null || parameters.Count == 0) {
            return false;
        }

        Type? firstType = parameters[0].StaticType;
        return firstType is not null && typeof(PowerShellHtmlRoute).IsAssignableFrom(firstType);
    }
}

/// <summary>
/// PowerShell-friendly Playwright route wrapper that records the requested route action.
/// </summary>
public sealed class PowerShellHtmlRoute {
    private readonly IRoute _route;
    private Func<Task>? _action;

    /// <summary>Creates a route wrapper for scriptblock handlers.</summary>
    public PowerShellHtmlRoute(IRoute route) {
        _route = route ?? throw new ArgumentNullException(nameof(route));
    }

    /// <summary>Original Playwright route for advanced scenarios.</summary>
    public IRoute Route => _route;

    /// <summary>Request associated with the route.</summary>
    public IRequest Request => _route.Request;

    /// <summary>Records a fulfill action for the route.</summary>
    public Task FulfillAsync(RouteFulfillOptions options) {
        _action = () => _route.FulfillAsync(options);
        return Task.CompletedTask;
    }

    /// <summary>Records an abort action for the route.</summary>
    public Task AbortAsync(string? errorCode = null) {
        _action = () => _route.AbortAsync(errorCode);
        return Task.CompletedTask;
    }

    /// <summary>Records a continue action for the route.</summary>
    public Task ContinueAsync(RouteContinueOptions? options = null) {
        _action = () => _route.ContinueAsync(options);
        return Task.CompletedTask;
    }

    /// <summary>Records a fallback action for the route.</summary>
    public Task FallbackAsync(RouteFallbackOptions? options = null) {
        _action = () => _route.FallbackAsync(options);
        return Task.CompletedTask;
    }

    internal Task ExecuteAsync() => _action is null ? Task.CompletedTask : _action();
}
