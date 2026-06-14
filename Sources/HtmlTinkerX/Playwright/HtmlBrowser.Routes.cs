using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

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
    public static Task RegisterRouteAsync(IPage page, string pattern, Func<IRoute, Task> handler, CancellationToken cancellationToken = default) {
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
    public static Task UnregisterRouteAsync(IPage page, string pattern, Func<IRoute, Task>? handler = null, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return handler is null ? page.UnrouteAsync(pattern) : page.UnrouteAsync(pattern, handler);
    }

    /// <summary>
    /// Removes a previously registered route handler from the session page.
    /// </summary>
    public static Task UnregisterRouteAsync(HtmlBrowserSession session, string pattern, Func<IRoute, Task>? handler = null, CancellationToken cancellationToken = default)
        => UnregisterRouteAsync(session.Page, pattern, handler, cancellationToken);

    /// <summary>
    /// Registers pre-navigation route handlers that abort matching resource types or URL patterns.
    /// </summary>
    /// <param name="page">Target page.</param>
    /// <param name="resourceTypes">Browser resource types to abort, such as Image, Media, Font, or Stylesheet.</param>
    /// <param name="patterns">Playwright URL glob patterns to abort, such as **/analytics/**.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Descriptions of blocking rules registered on the page.</returns>
    public static async Task<IReadOnlyList<string>> ApplyResourceBlockingAsync(
        IPage page,
        IEnumerable<HtmlNetworkResourceType>? resourceTypes = null,
        IEnumerable<string>? patterns = null,
        CancellationToken cancellationToken = default) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }

        List<string> applied = new();
        HashSet<HtmlNetworkResourceType> blockedTypes = NormalizeResourceTypes(resourceTypes);
        if (blockedTypes.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.RouteAsync("**/*", route => {
                HtmlNetworkResourceType resourceType = HtmlEnumParser.ParseNetworkResourceType(route.Request.ResourceType);
                return blockedTypes.Contains(resourceType)
                    ? route.AbortAsync()
                    : route.ContinueAsync();
            }).ConfigureAwait(false);
            applied.AddRange(blockedTypes.Select(static type => $"Blocked resource type: {type}"));
        }

        foreach (string pattern in NormalizeRoutePatterns(patterns)) {
            cancellationToken.ThrowIfCancellationRequested();
            await RegisterRouteAsync(page, pattern, route => route.AbortAsync(), cancellationToken).ConfigureAwait(false);
            applied.Add($"Blocked pattern: {pattern}");
        }

        return applied;
    }

    private static HashSet<HtmlNetworkResourceType> NormalizeResourceTypes(IEnumerable<HtmlNetworkResourceType>? resourceTypes) =>
        resourceTypes == null
            ? new HashSet<HtmlNetworkResourceType>()
            : new HashSet<HtmlNetworkResourceType>(
                resourceTypes.Where(static type => type != HtmlNetworkResourceType.Document),
                EqualityComparer<HtmlNetworkResourceType>.Default);

    private static IEnumerable<string> NormalizeRoutePatterns(IEnumerable<string>? patterns) =>
        patterns == null
            ? Array.Empty<string>()
            : patterns.Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(static pattern => pattern.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
}
