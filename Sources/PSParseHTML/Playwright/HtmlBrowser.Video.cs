using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Starts a new browser session with video recording enabled.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="videoPath">Output path for the recorded video.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Reinstall the browser runtime.</param>
    /// <param name="username">Optional basic authentication user name.</param>
    /// <param name="password">Optional basic authentication password.</param>
    /// <param name="formLogin">Login form information when authentication is required.</param>
    /// <param name="headless">Run the browser in headless mode.</param>
    /// <param name="slowMo">Delay between Playwright actions in milliseconds.</param>
    /// <param name="width">Video width.</param>
    /// <param name="height">Video height.</param>
    /// <param name="userAgent">Custom user agent string.</param>
    /// <param name="viewportWidth">Width of the browser viewport.</param>
    /// <param name="viewportHeight">Height of the browser viewport.</param>
    /// <param name="deviceScaleFactor">Device scale factor.</param>
    /// <param name="geoLatitude">Latitude used for geolocation.</param>
    /// <param name="geoLongitude">Longitude used for geolocation.</param>
    /// <param name="timezone">Time zone identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static Task<HtmlBrowserSession> StartVideoRecordingAsync(
        string url,
        string videoPath,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        int width = 800,
        int height = 600,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null,
        CancellationToken cancellationToken = default)
        => OpenSessionAsync(url, browser, clean, username, password, formLogin, headless, slowMo, videoPath, width, height, null, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, proxy: null, proxyUsername: null, proxyPassword: null, geoLatitude: geoLatitude, geoLongitude: geoLongitude, timezone: timezone, cancellationToken: cancellationToken);

    /// <summary>
    /// Starts a video recording session based on an existing <see cref="HtmlBrowserSession"/>.
    /// </summary>
    /// <param name="session">Existing browser session.</param>
    /// <param name="videoPath">Output path for the recorded video.</param>
    /// <param name="headless">Run the new session in headless mode.</param>
    /// <param name="slowMo">Delay between Playwright actions in milliseconds.</param>
    /// <param name="width">Video width.</param>
    /// <param name="height">Video height.</param>
    /// <param name="userAgent">Custom user agent string.</param>
    /// <param name="viewportWidth">Width of the browser viewport.</param>
    /// <param name="viewportHeight">Height of the browser viewport.</param>
    /// <param name="deviceScaleFactor">Device scale factor.</param>
    /// <param name="geoLatitude">Latitude used for geolocation.</param>
    /// <param name="geoLongitude">Longitude used for geolocation.</param>
    /// <param name="timezone">Time zone identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task<HtmlBrowserSession> StartVideoRecordingAsync(
        HtmlBrowserSession session,
        string videoPath,
        bool headless = true,
        int slowMo = 0,
        int width = 800,
        int height = 600,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null,
        CancellationToken cancellationToken = default) {
        string temp = Path.GetTempFileName();
        await session.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = temp }).ConfigureAwait(false);
        string url = session.Page.Url;
        HtmlBrowserEngine engine = session.Browser.BrowserType.Name switch {
            "firefox" => HtmlBrowserEngine.Firefox,
            "webkit" => HtmlBrowserEngine.Webkit,
            _ => HtmlBrowserEngine.Chromium
        };

        HtmlBrowserSession newSession = await OpenSessionAsync(
            url,
            engine,
            clean: false,
            username: null,
            password: null,
            formLogin: null,
            headless: headless,
            slowMo: slowMo,
            videoPath: videoPath,
            videoWidth: width,
            videoHeight: height,
            storageStatePath: temp,
            userAgent: userAgent,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight,
            deviceScaleFactor: deviceScaleFactor,
            proxy: null,
            proxyUsername: null,
            proxyPassword: null,
            geoLatitude: geoLatitude,
            geoLongitude: geoLongitude,
            timezone: timezone,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        File.Delete(temp);
        return newSession;
    }

    /// <summary>
    /// Stops the specified video recording session and saves the file.
    /// </summary>
    /// <param name="session">Session that was recording video.</param>
    /// <param name="path">Optional path where the video should be saved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task StopVideoRecordingAsync(HtmlBrowserSession session, string? path = null, CancellationToken cancellationToken = default) {
        if (path != null) {
            session.VideoPath = path;
        }

        if (session.Video != null && string.IsNullOrEmpty(session.VideoPath)) {
            throw new System.ArgumentException("Output path required.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await session.DisposeAsync().ConfigureAwait(false);
    }
}
