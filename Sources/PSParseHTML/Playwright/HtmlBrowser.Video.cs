using System.IO;
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
        int height = 600)
        => OpenSessionAsync(url, browser, clean, username, password, formLogin, headless, slowMo, videoPath, width, height);

    /// <summary>
    /// Stops the specified video recording session and saves the file.
    /// </summary>
    public static async Task StopVideoRecordingAsync(HtmlBrowserSession session, string? path = null) {
        string outFile = path ?? session.VideoPath ?? throw new System.ArgumentException("Output path required.");
        IVideo? video = session.Video;
        if (video != null) {
            await session.Context.CloseAsync().ConfigureAwait(false);
            string fullPath = HtmlUtilities.ResolvePath(outFile);
            await video.SaveAsAsync(fullPath).ConfigureAwait(false);
            await session.Browser.CloseAsync().ConfigureAwait(false);
            session.Playwright.Dispose();
        } else {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
