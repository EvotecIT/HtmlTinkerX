using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Registers a network route handler on the specified page.
    /// </summary>
public static Task RegisterRouteAsync(IPage page, string pattern, Func<IRoute, Task> handler, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    return page.RouteAsync(pattern, handler);
}

    /// <summary>
    /// Registers a network route handler on the session page.
    /// </summary>
public static Task RegisterRouteAsync(HtmlBrowserSession session, string pattern, Func<IRoute, Task> handler, CancellationToken cancellationToken = default)
    => RegisterRouteAsync(session.Page, pattern, handler, cancellationToken);

    /// <summary>
    /// Removes a previously registered route handler from the page.
    /// </summary>
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
