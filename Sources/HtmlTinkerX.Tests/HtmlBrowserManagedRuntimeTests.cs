using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserManagedRuntimeTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task ManagedGenerationRecordsVideoWithItsOwnFfmpeg(HtmlBrowserEngine engine) {
        string root = Path.Combine(Path.GetTempPath(), "htmltinkerx-video-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string video = Path.Combine(root, "recording.webm");
        try {
            await using HtmlBrowserSession session = await HtmlBrowser.StartVideoRecordingAsync("about:blank", video, engine, width: 160, height: 120);
            await session.Page.SetContentAsync("<body style='background:teal'>Verified video</body>");
            await session.Page.ScreenshotAsync();
            await HtmlBrowser.StopVideoRecordingAsync(session);
            Assert.True(File.Exists(video));
            Assert.True(new FileInfo(video).Length > 0);
        } finally { Directory.Delete(root, true); }
    }
}
