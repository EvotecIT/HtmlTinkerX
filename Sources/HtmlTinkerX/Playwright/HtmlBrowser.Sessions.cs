using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>Creates a session for installing routes before its first destination. Configured form login still runs.</summary>
    public static Task<HtmlBrowserSession> OpenSessionForNavigationAsync(HtmlBrowserLaunchOptions options, CancellationToken cancellationToken = default) {
        if (options == null) throw new ArgumentNullException(nameof(options));
        return CreatePageAsync("about:blank", options, cancellationToken, skipInitialNavigation: true);
    }

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> using reusable launch options and navigates to the specified URL.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="options">Browser launch, profile, emulation, and network options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<HtmlBrowserSession> OpenSessionAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }

        return CreatePageAsync(url, options, cancellationToken);
    }

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> using reusable launch options and navigates to the specified URL.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="options">Browser launch, profile, emulation, and network options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<HtmlBrowserSession> OpenSessionWithOptionsAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default)
        => OpenSessionAsync(url, options, cancellationToken);

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> and navigates to the specified URL.
    /// </summary>
    public static Task<HtmlBrowserSession> OpenSessionAsync(
        string url,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? videoPath = null,
        int videoWidth = 800,
        int videoHeight = 600,
        string? storageStatePath = null,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null,
        int timeout = 10000,
        CancellationToken cancellationToken = default)
        => OpenSessionWithOptionsAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            videoPath,
            videoWidth,
            videoHeight,
            storageStatePath,
            userAgent,
            viewportWidth,
            viewportHeight,
            deviceScaleFactor,
            proxy,
            proxyUsername,
            proxyPassword,
            geoLatitude,
            geoLongitude,
            timezone,
            blockResourceTypes: null,
            blockResourcePatterns: null,
            loadState: HtmlBrowserLoadState.NetworkIdle,
            timeout,
            cancellationToken);

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> and navigates to the specified URL.
    /// </summary>
    public static Task<HtmlBrowserSession> OpenSessionWithOptionsAsync(
        string url,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? videoPath = null,
        int videoWidth = 800,
        int videoHeight = 600,
        string? storageStatePath = null,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null,
        IEnumerable<HtmlNetworkResourceType>? blockResourceTypes = null,
        IEnumerable<string>? blockResourcePatterns = null,
        HtmlBrowserLoadState loadState = HtmlBrowserLoadState.NetworkIdle,
        int timeout = 10000,
        CancellationToken cancellationToken = default)
        => OpenSessionAsync(
            url,
            HtmlBrowserLaunchOptions.FromLegacyParameters(
                browser,
                clean,
                username,
                password,
                formLogin,
                headless,
                slowMo,
                videoPath,
                videoWidth,
                videoHeight,
                storageStatePath,
                userAgent,
                viewportWidth,
                viewportHeight,
                deviceScaleFactor,
                proxy,
                proxyUsername,
                proxyPassword,
                geoLatitude,
                geoLongitude,
                timezone,
                blockResourceTypes,
                blockResourcePatterns,
                loadState,
                timeout),
            cancellationToken);

    /// <summary>
    /// Disposes the specified browser session.
    /// </summary>
    public static async Task CloseSessionAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default) {
        if (session != null) {
            cancellationToken.ThrowIfCancellationRequested();
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
