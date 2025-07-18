namespace HtmlTinkerX;

using Microsoft.Playwright;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Helper methods for exporting and importing browser session state.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Saves cookies and storage state of the provided session to a file.
    /// </summary>
    /// <param name="session">Browser session to export.</param>
    /// <param name="path">File path where the session state should be stored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task ExportSessionAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return session.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = fullPath });
    }

    /// <summary>
    /// Saves cookies and storage state of the provided session to a file. This
    /// is an alias for <see cref="ExportSessionAsync"/>.
    /// </summary>
    public static Task ExportBrowserStateAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default)
        => ExportSessionAsync(session, path, cancellationToken);

    /// <summary>
    /// Creates a new session using cookies and storage state saved with <see cref="ExportSessionAsync"/>.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="statePath">Path to the previously exported session state.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Force re-download of browser runtimes.</param>
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="slowMo">Slow motion delay in milliseconds.</param>
    /// <param name="userAgent">Custom user agent string.</param>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    /// <param name="deviceScaleFactor">Device scale factor.</param>
    /// <param name="proxy">Proxy server URL.</param>
    /// <param name="proxyUsername">Proxy username.</param>
    /// <param name="proxyPassword">Proxy password.</param>
    /// <param name="geoLatitude">Latitude for geolocation.</param>
    /// <param name="geoLongitude">Longitude for geolocation.</param>
    /// <param name="timezone">Timezone identifier.</param>
    /// <param name="timeout">Navigation timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<HtmlBrowserSession> ImportSessionAsync(
        string url,
        string statePath,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        bool headless = true,
        int slowMo = 0,
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
        => OpenSessionAsync(
            url,
            browser,
            clean,
            username: null,
            password: null,
            formLogin: null,
            headless,
            slowMo,
            videoPath: null,
            videoWidth: 800,
            videoHeight: 600,
            storageStatePath: HtmlUtilities.ResolvePath(statePath),
            userAgent: userAgent,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight,
            deviceScaleFactor: deviceScaleFactor,
            proxy: proxy,
            proxyUsername: proxyUsername,
            proxyPassword: proxyPassword,
            geoLatitude: geoLatitude,
            geoLongitude: geoLongitude,
            timezone: timezone,
            timeout: timeout,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Creates a new session using cookies and storage state saved with
    /// <see cref="ExportBrowserStateAsync"/>. This is an alias for
    /// <see cref="ImportSessionAsync"/>.
    /// </summary>
    public static Task<HtmlBrowserSession> ImportBrowserStateAsync(
        string url,
        string statePath,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        bool headless = true,
        int slowMo = 0,
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
        => ImportSessionAsync(
            url,
            statePath,
            browser,
            clean,
            headless,
            slowMo,
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
            timeout,
            cancellationToken);
}