using HtmlTinkerX;
using Microsoft.Playwright;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

            await PowerShellHtmlRoute.ExecuteRecordedAsync(route).ConfigureAwait(false);
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
    private sealed class RouteAction {
        internal Func<Task>? Action { get; set; }
    }

    private static readonly ConditionalWeakTable<IRoute, RouteAction> RecordedActions = new();
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

    internal static void RecordAction(IRoute route, Func<Task> action) {
        RouteAction holder = RecordedActions.GetValue(route, _ => new RouteAction());
        holder.Action = action;
    }

    internal static Task ExecuteRecordedAsync(IRoute route) {
        if (!RecordedActions.TryGetValue(route, out RouteAction? holder) || holder.Action is null) {
            return Task.CompletedTask;
        }

        RecordedActions.Remove(route);
        return holder.Action();
    }
}

/// <summary>
/// Cmdlet that fulfills an intercepted browser route with a mocked response.
/// </summary>
/// <example>
///   <summary>Return a JSON response from a route handler</summary>
///   <code>Register-HtmlRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
///     param($route)
///     Complete-HtmlRoute -Route $route -Status 200 -ContentType 'application/json' -Body '{"status":"ok"}'
/// }</code>
/// </example>
/// <example>
///   <summary>Return an object as JSON</summary>
///   <code>$route | Complete-HtmlRoute -Json @{ status = 'ok'; count = 1 }</code>
/// </example>
[Cmdlet(VerbsLifecycle.Complete, "HtmlRoute", DefaultParameterSetName = ParameterSetBody)]
public sealed class CmdletCompleteHtmlRoute : PSCmdlet {
    private const string ParameterSetBody = "Body";
    private const string ParameterSetBodyBytes = "BodyBytes";
    private const string ParameterSetJson = "Json";
    private const string ParameterSetPath = "Path";

    /// <summary>Route object received by a Register-HtmlRoute script block.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public object Route { get; set; } = null!;

    /// <summary>HTTP status code to return.</summary>
    [Parameter]
    public int Status { get; set; } = 200;

    /// <summary>Response content type.</summary>
    [Parameter]
    public string? ContentType { get; set; }

    /// <summary>Response headers.</summary>
    [Parameter]
    public IDictionary? Header { get; set; }

    /// <summary>Response options supplied as a hashtable.</summary>
    [Parameter]
    [Alias("Option")]
    public IDictionary? Options { get; set; }

    /// <summary>Text response body.</summary>
    [Parameter(Position = 1, ParameterSetName = ParameterSetBody)]
    public string? Body { get; set; }

    /// <summary>Binary response body.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetBodyBytes)]
    public byte[]? BodyBytes { get; set; }

    /// <summary>Object serialized by Playwright as a JSON response.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetJson)]
    public object? Json { get; set; }

    /// <summary>File path used as the response body.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath)]
    public string? Path { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        object routeInput = Route ?? throw new PSArgumentNullException(nameof(Route));
        if (routeInput is PSObject psObject) {
            routeInput = psObject.BaseObject;
        }

        RouteFulfillOptions options = CreateOptions();
        switch (routeInput) {
            case PowerShellHtmlRoute powerShellRoute:
                _ = powerShellRoute.FulfillAsync(options);
                break;
            case IRoute playwrightRoute:
                PowerShellHtmlRoute.RecordAction(playwrightRoute, () => playwrightRoute.FulfillAsync(options));
                break;
            default:
                throw new PSArgumentException("Route must be a PowerShellHtmlRoute or Microsoft.Playwright.IRoute instance.", nameof(Route));
        }
    }

    private RouteFulfillOptions CreateOptions() {
        RouteFulfillOptions options = new();
        if (Options is not null) {
            ApplyOptions(options, Options);
        }

        if (Options is null || MyInvocation.BoundParameters.ContainsKey(nameof(Status))) {
            options.Status = Status;
        }

        if (!string.IsNullOrWhiteSpace(ContentType)) {
            options.ContentType = ContentType;
        }

        if (Header is not null) {
            Dictionary<string, string> headers = ConvertHeaders(Header);
            if (headers.Count > 0) {
                options.Headers = headers;
            }
        }

        switch (ParameterSetName) {
            case ParameterSetBodyBytes:
                options.BodyBytes = BodyBytes;
                break;
            case ParameterSetJson:
                ApplyJson(options, Json);
                break;
            case ParameterSetPath:
                options.Path = Path!.ToFullPath();
                break;
            default:
                if (MyInvocation.BoundParameters.ContainsKey(nameof(Body))) {
                    options.Body = Body;
                }

                break;
        }

        return options;
    }

    private static void ApplyOptions(RouteFulfillOptions options, IDictionary optionValues) {
        foreach (DictionaryEntry entry in optionValues) {
            if (entry.Key is null) {
                continue;
            }

            string key = LanguagePrimitives.ConvertTo<string>(entry.Key);
            object? rawValue = entry.Value;
            object? value = rawValue is PSObject psObject ? psObject.BaseObject : rawValue;
            switch (key.ToUpperInvariant()) {
                case "STATUS":
                    options.Status = LanguagePrimitives.ConvertTo<int>(value);
                    break;
                case "CONTENTTYPE":
                    options.ContentType = LanguagePrimitives.ConvertTo<string>(value);
                    break;
                case "BODY":
                    options.Body = LanguagePrimitives.ConvertTo<string>(value);
                    break;
                case "BODYBYTES":
                    options.BodyBytes = LanguagePrimitives.ConvertTo<byte[]>(value);
                    break;
                case "JSON":
                    ApplyJson(options, rawValue);
                    break;
                case "PATH":
                    options.Path = LanguagePrimitives.ConvertTo<string>(value).ToFullPath();
                    break;
                case "RESPONSE":
                    if (value is not null) {
                        options.Response = LanguagePrimitives.ConvertTo<IAPIResponse>(value);
                    }

                    break;
                case "HEADER":
                case "HEADERS":
                    if (value is IDictionary headers) {
                        options.Headers = ConvertHeaders(headers);
                    }

                    break;
            }
        }
    }

    private static Dictionary<string, string> ConvertHeaders(IDictionary headers) {
        Dictionary<string, string> converted = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in headers) {
            if (entry.Key is null || entry.Value is null) {
                continue;
            }

            converted[LanguagePrimitives.ConvertTo<string>(entry.Key)] = LanguagePrimitives.ConvertTo<string>(entry.Value);
        }

        return converted;
    }

    private static void ApplyJson(RouteFulfillOptions options, object? value) {
        options.Body = JsonSerializer.Serialize(ConvertJsonValue(value));
        options.ContentType ??= "application/json";
    }

    private static object? ConvertJsonValue(object? value) {
        if (value is null) {
            return null;
        }

        if (value is PSObject psObject) {
            if (HasNoteProperties(psObject)) {
                Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);
                foreach (PSPropertyInfo property in psObject.Properties) {
                    if (property.MemberType == PSMemberTypes.NoteProperty && property.IsGettable) {
                        properties[property.Name] = ConvertJsonValue(property.Value);
                    }
                }

                return properties;
            }

            return ConvertJsonValue(psObject.BaseObject);
        }

        if (value is IDictionary dictionary) {
            Dictionary<string, object?> converted = new(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary) {
                if (entry.Key is null) {
                    continue;
                }

                converted[LanguagePrimitives.ConvertTo<string>(entry.Key)] = ConvertJsonValue(entry.Value);
            }

            return converted;
        }

        if (value is IEnumerable enumerable and not string and not byte[]) {
            List<object?> converted = new();
            foreach (object? item in enumerable) {
                converted.Add(ConvertJsonValue(item));
            }

            return converted;
        }

        return value;
    }

    private static bool HasNoteProperties(PSObject psObject) {
        foreach (PSPropertyInfo property in psObject.Properties) {
            if (property.MemberType == PSMemberTypes.NoteProperty) {
                return true;
            }
        }

        return false;
    }
}
