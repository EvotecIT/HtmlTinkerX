using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Js;
using System;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for executing JavaScript against HTML using AngleSharp.Js.
/// </summary>
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
    public static async Task<T?> RunAsync<T>(string html, string script) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        if (script == null) {
            throw new ArgumentNullException(nameof(script));
        }

        var config = Configuration.Default.WithJs();
        var context = BrowsingContext.New(config);
        var document = await context
            .OpenAsync(req => req.Content(html))
            .WaitUntilAvailable()
            .ConfigureAwait(false);
        object? result = document.ExecuteScript(script);
        return result is T variable ? variable : (T?)Convert.ChangeType(result, typeof(T));
    }
}