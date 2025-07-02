using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Registers a network route handler on the specified page.
    /// </summary>
    /// <param name="page">Target page.</param>
    /// <param name="pattern">Matching pattern for the route.</param>
    /// <param name="handler">Handler invoked for matching requests.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static Task RegisterRouteAsync(IPage page, string pattern, Func<IRoute, Task> handler, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return page.RouteAsync(pattern, handler);
    }

    /// <summary>
    /// Registers a network route handler on the session page.
    /// </summary>
    /// <param name="session">Browser session to register on.</param>
    /// <param name="pattern">Matching pattern for the route.</param>
    /// <param name="handler">Handler invoked for matching requests.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static Task RegisterRouteAsync(HtmlBrowserSession session, string pattern, Func<IRoute, Task> handler, CancellationToken cancellationToken = default)
        => RegisterRouteAsync(session.Page, pattern, handler, cancellationToken);

    /// <summary>
    /// Removes a previously registered route handler from the page.
    /// </summary>
    /// <param name="page">Target page.</param>
    /// <param name="pattern">Pattern used when registering the route.</param>
    /// <param name="handler">Optional handler instance.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static Task UnregisterRouteAsync(IPage page, string pattern, Func<IRoute, Task>? handler = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return handler is null ? page.UnrouteAsync(pattern) : page.UnrouteAsync(pattern, handler);
    }

    /// <summary>
    /// Removes a previously registered route handler from the session page.
    /// </summary>
public static Task UnregisterRouteAsync(HtmlBrowserSession session, string pattern, Func<IRoute, Task>? handler = null, CancellationToken cancellationToken = default)
    => UnregisterRouteAsync(session.Page, pattern, handler, cancellationToken);
}
