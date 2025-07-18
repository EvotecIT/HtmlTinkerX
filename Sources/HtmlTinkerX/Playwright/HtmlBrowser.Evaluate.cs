using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for running JavaScript in a browser session.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Executes arbitrary JavaScript in the context of the current page.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="session">Browser session.</param>
    /// <param name="script">JavaScript code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Value returned by the script.</returns>
    public static Task<T?> EvaluateAsync<T>(HtmlBrowserSession session, string script, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return session.Page.EvaluateAsync<T?>(script);
    }
}