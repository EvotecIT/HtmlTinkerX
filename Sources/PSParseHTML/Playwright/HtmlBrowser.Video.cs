using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Starts a new browser session with video recording enabled.
    /// </summary>
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
    public static async Task StopVideoRecordingAsync(HtmlBrowserSession session, string? path = null, CancellationToken cancellationToken = default) {
        string outFile = path ?? session.VideoPath ?? throw new System.ArgumentException("Output path required.");
        IVideo? video = session.Video;
        if (video != null) {
            cancellationToken.ThrowIfCancellationRequested();
            await session.Context.CloseAsync().ConfigureAwait(false);
            string fullPath = HtmlUtilities.ResolvePath(outFile);
            await video.SaveAsAsync(fullPath).ConfigureAwait(false);
            try {
                string tempPath = await video.PathAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(tempPath) &&
                    !string.Equals(tempPath, fullPath, System.StringComparison.OrdinalIgnoreCase) &&
                    System.IO.File.Exists(tempPath)) {
                    System.IO.File.Delete(tempPath);
                }
            } catch {
                // Ignore cleanup errors
            }
            await session.Browser.CloseAsync().ConfigureAwait(false);
            session.Playwright.Dispose();
        } else {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
