using System;
using System.IO;
using System.Threading.Tasks;
using ChartForgeX.Raster;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserScreenshotLiveTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureScreenshotAsync_WithLivePageAndOptionalAnnotations_PreservesFramePixels(bool annotate) {
        string directory = Path.Combine(Path.GetTempPath(), $"screenshot-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "capture.png");
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
            Browser = HtmlBrowserEngine.Chromium,
            Headless = true,
            ViewportWidth = 320,
            ViewportHeight = 220
        });
        try {
            await session.Page.SetContentAsync("""
<!doctype html><html><body style="margin:0;background:#ffffff">
<div id="target" style="position:absolute;left:40px;top:80px;width:120px;height:70px;background:#0000ff"></div>
<iframe title="Screenshot frame" style="position:absolute;left:180px;top:80px;width:100px;height:70px;border:0"
        srcdoc="<body style='margin:0;background:#00ff00'>Frame content</body>"></iframe>
</body></html>
""");
            await session.Page.Locator("iframe").ContentFrame.Locator("body").WaitForAsync();
            await HtmlBrowser.CaptureScreenshotAsync(session.Page, path, new ScreenshotOptions {
                HighlightSelectors = annotate ? new[] { "#target" } : null,
                OverlayText = annotate ? "Screenshot proof" : null
            });

            RgbaImage image = RasterImageDecoder.Decode(File.ReadAllBytes(path));
            Assert.Equal(320, image.Width);
            Assert.Equal(220, image.Height);
            Assert.Equal(((byte)0, (byte)0, (byte)255), Pixel(image, 100, 110));
            Assert.Equal(((byte)0, (byte)255, (byte)0), Pixel(image, 210, 120));
            Assert.Equal(((byte)255, (byte)255, (byte)255), Pixel(image, 310, 210));
            Assert.Equal(annotate ? (byte)255 : (byte)0, Channel(image, 40, 100, 0));
            Assert.Equal(annotate, HasRedOverlayText(image));
        } finally {
            await HtmlBrowser.CloseSessionAsync(session);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static byte Channel(RgbaImage image, int x, int y, int channel) => image.Pixels[(y * image.Width + x) * 4 + channel];

    private static (byte R, byte G, byte B) Pixel(RgbaImage image, int x, int y) =>
        (Channel(image, x, y, 0), Channel(image, x, y, 1), Channel(image, x, y, 2));

    private static bool HasRedOverlayText(RgbaImage image) {
        for (int y = 10; y < 50; y++) {
            for (int x = 10; x < 310; x++) {
                if (Channel(image, x, y, 0) > 180 && Channel(image, x, y, 1) < 80 && Channel(image, x, y, 2) < 80) return true;
            }
        }
        return false;
    }
}
