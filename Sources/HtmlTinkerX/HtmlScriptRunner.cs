using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Js;
using AngleSharp.Scripting;
using AngleSharp.Browser.Dom.Events;
using Jint;
using Jint.Constraints;
using Jint.Runtime;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for executing JavaScript against HTML using AngleSharp.Js.
/// </summary>
/// <remarks>
/// The default overload operates on the supplied markup only. It does not register document
/// loaders, requesters, WebSockets, or other network-capable browser services. Callers can use
/// the browsing-context overload to opt into those capabilities explicitly.
/// </remarks>
public static class HtmlScriptRunner {
    /// <summary>
    /// Loads the provided HTML markup and executes JavaScript in its context.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="html">HTML markup to load.</param>
    /// <param name="script">JavaScript code to execute.</param>
    /// <returns>Value returned by the script.</returns>
    /// <example>
    /// <code>
    /// var result = await HtmlScriptRunner.RunAsync&lt;int&gt;("&lt;div id='a'&gt;&lt;/div&gt;",
    ///     "document.getElementById('a').textContent = '1'; 1;");
    /// </code>
    /// </example>
    public static Task<T?> RunAsync<T>(string html, string script) {
        return RunAsync<T>(html, script, new HtmlScriptRunOptions(), CancellationToken.None);
    }

    /// <summary>Executes JavaScript in an owned context with input, time, statement and allocation limits.</summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="html">HTML markup to load.</param>
    /// <param name="script">Explicit JavaScript expression or statements; the final expression is returned.</param>
    /// <param name="options">Execution policy. Page scripts run by default under the same operation deadline.</param>
    /// <param name="cancellationToken">Cancellation covering loading and all script execution.</param>
    /// <returns>The requested result after closing the owned context.</returns>
    /// <remarks>Statement and managed-allocation limits apply to each Jint execution. The timeout spans the entire operation. Default contexts do not register network services.</remarks>
    public static async Task<T?> RunAsync<T>(string html, string script, HtmlScriptRunOptions options, CancellationToken cancellationToken) {
        if (html == null) throw new ArgumentNullException(nameof(html));
        if (script == null) throw new ArgumentNullException(nameof(script));
        if (options == null) throw new ArgumentNullException(nameof(options));
        HtmlScriptRunOptions policy = options.Snapshot();
        if (html.Length > policy.MaximumHtmlCharacters) throw new ArgumentException("HTML exceeds the configured character limit.", nameof(html));
        if (script.Length > policy.MaximumScriptCharacters) throw new ArgumentException("Script exceeds the configured character limit.", nameof(script));
        cancellationToken.ThrowIfCancellationRequested();

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (policy.ExecutionTimeout != Timeout.InfiniteTimeSpan) operationCancellation.CancelAfter(policy.ExecutionTimeout);
        var deadline = new OperationDeadlineConstraint();
        deadline.Begin(policy.ExecutionTimeout, operationCancellation.Token);
        var configuration = Configuration.Default.With(new EngineCreator((window, engineOptions) =>
            new Engine(engineOptions.Constraint(deadline)
                .MaxStatements(policy.MaximumStatements).LimitMemory(policy.MaximumMemoryBytes))));
        // An inert page uses the same native service for the explicitly requested script only.
        var explicitService = policy.ExecutePageScripts ? null : new JsScriptingService();
        if (policy.ExecutePageScripts) configuration = configuration.WithJs();
        using var context = BrowsingContext.New(configuration);
        Exception? pageBudgetFailure = null;
        context.AddEventListener("error", (_, eventArgs) => {
            if (eventArgs is TrackEvent tracked && tracked.Error is Exception exception &&
                (exception is StatementsCountOverflowException || exception is MemoryLimitExceededException ||
                 exception is TimeoutException || exception is OperationCanceledException)) {
                Interlocked.CompareExchange(ref pageBudgetFailure, exception, null);
            }
        });

        try {
            var document = await context.OpenAsync(request => request.Content(html), operationCancellation.Token)
                .WaitUntilAvailable().ConfigureAwait(false);
            Exception? failure = Volatile.Read(ref pageBudgetFailure);
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            deadline.Check();
            object? result = explicitService == null ? document.ExecuteScript(script)
                : explicitService.EvaluateScript(document, script, "text/javascript", null);
            failure = Volatile.Read(ref pageBudgetFailure);
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            deadline.Check();
            return result is T value ? value : (T?)Convert.ChangeType(result, typeof(T));
        } catch (OperationCanceledException exception) when (operationCancellation.IsCancellationRequested) {
            if (cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException("DOM script execution was canceled.", exception, cancellationToken);
            }
            throw new TimeoutException("DOM script execution exceeded its operation timeout.", exception);
        } finally {
            // Leave the owned engines canceled while their document and any scheduled callbacks close.
            operationCancellation.Cancel();
        }
    }

    /// <summary>
    /// Loads the provided HTML markup and executes JavaScript using a caller-owned
    /// AngleSharp browsing context.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="html">HTML markup to load.</param>
    /// <param name="script">JavaScript code to execute.</param>
    /// <param name="context">
    /// Browsing context that controls available services and document lifetime. Its
    /// configuration must include AngleSharp.Js. Registering loaders or AngleSharp.Io
    /// requesters can allow scripts to perform I/O.
    /// This overload retains the caller's execution policy and does not dispose the context.
    /// Configure Jint constraints through an EngineCreator service before creating that context.
    /// </param>
    /// <returns>Value returned by the script.</returns>
    public static async Task<T?> RunAsync<T>(string html, string script, IBrowsingContext context) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        if (script == null) {
            throw new ArgumentNullException(nameof(script));
        }
        if (context == null) {
            throw new ArgumentNullException(nameof(context));
        }
        if (context.GetService<JsScriptingService>() == null) {
            throw new ArgumentException("The browsing context must register AngleSharp.Js by calling WithJs() on its configuration.", nameof(context));
        }

        var document = await context
            .OpenAsync(req => req.Content(html))
            .WaitUntilAvailable()
            .ConfigureAwait(false);
        object? result = document.ExecuteScript(script);
        return result is T variable ? variable : (T?)Convert.ChangeType(result, typeof(T));
    }
}
