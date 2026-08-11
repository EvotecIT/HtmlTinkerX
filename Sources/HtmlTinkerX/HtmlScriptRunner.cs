using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Js;
using AngleSharp.Scripting;
using System;
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
    ///     "document.getElementById('a').textContent = '1'; return 1;");
    /// </code>
    /// </example>
    public static Task<T?> RunAsync<T>(string html, string script) {
        return RunAsync<T>(html, script, BrowsingContext.New(Configuration.Default.WithJs()));
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
