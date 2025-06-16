namespace PSParseHTML;

using Microsoft.Playwright;
using System.IO;
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
    public static Task ExportSessionAsync(HtmlBrowserSession session, string path) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        return session.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = fullPath });
    }

    /// <summary>
    /// Creates a new session using cookies and storage state saved with <see cref="ExportSessionAsync"/>.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="statePath">Path to the previously exported session state.</param>
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
        string? timezone = null)
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
            timezone: timezone);
}
