using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for starting and stopping video recording.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Starts a new browser session with video recording enabled.
    /// </summary>
    /// <inheritdoc cref="HtmlBrowser.StartVideoRecordingAsync(string,string,HtmlBrowserEngine,bool,string?,string?,HtmlFormLogin?,bool,int,int,int,string?,int?,int?,float?,double?,double?,string?,CancellationToken)"/>
    public static Task<HtmlBrowserSession> StartRecordingAsync(
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
        => StartVideoRecordingAsync(url, videoPath, browser, clean, username, password, formLogin, headless, slowMo, width, height, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, geoLatitude, geoLongitude, timezone, cancellationToken);

    /// <summary>
    /// Starts recording based on an existing session.
    /// </summary>
    /// <inheritdoc cref="HtmlBrowser.StartVideoRecordingAsync(HtmlBrowserSession,string,bool,int,int,int,string?,int?,int?,float?,double?,double?,string?,CancellationToken)"/>
    public static Task<HtmlBrowserSession> StartRecordingAsync(
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
        CancellationToken cancellationToken = default)
        => StartVideoRecordingAsync(session, videoPath, headless, slowMo, width, height, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, geoLatitude, geoLongitude, timezone, cancellationToken);

    /// <summary>
    /// Stops an active recording session and returns the saved video file path.
    /// </summary>
    /// <param name="session">Session that was recording.</param>
    /// <param name="path">Optional path where the video should be saved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Path to the saved video file.</returns>
    public static async Task<string> StopRecordingAsync(
        HtmlBrowserSession session,
        string? path = null,
        CancellationToken cancellationToken = default) {
        await StopVideoRecordingAsync(session, path, cancellationToken).ConfigureAwait(false);
        return HtmlUtilities.ResolvePath(session.VideoPath!);
    }
}
