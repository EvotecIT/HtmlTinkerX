using ChartForgeX;
using ChartForgeX.Raster;
using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Reflection;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserScreenshotTests {
    private static byte[] CreatePngImage() {
        var pixels = new byte[20 * 20 * 4];
        for (int y = 0; y < 20; y++) {
            for (int x = 0; x < 20; x++) {
                int offset = (y * 20 + x) * 4;
                pixels[offset] = 0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = 255;
                pixels[offset + 3] = 255;
            }
        }
        return new RgbaImage(20, 20, pixels).ToPng();
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ClipOptionsPassedToPlaywright() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "clip.png");
        var page = new Mock<IPage>();
        PageScreenshotOptions? options = null;
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .Callback<PageScreenshotOptions>(o => options = o)
            .ReturnsAsync(CreatePngImage());

        await HtmlBrowser.CaptureScreenshotAsync(
            page.Object,
            file,
            new ScreenshotOptions {
                ClipX = 5,
                ClipY = 10,
                ClipWidth = 50,
                ClipHeight = 20
            });

        Assert.NotNull(options);
        Assert.NotNull(options!.Clip);
        Assert.Equal(5, options.Clip.X);
        Assert.Equal(10, options.Clip.Y);
        Assert.Equal(50, options.Clip.Width);
        Assert.Equal(20, options.Clip.Height);
        Assert.True(File.Exists(file));

        File.Delete(file);
        Directory.Delete(dir);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_WithHighlightsAndOverlay_SavesModifiedImage() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "highlight.png");
        var page = new Mock<IPage>();
        var element = new Mock<IElementHandle>();
        element.Setup(e => e.BoundingBoxAsync())
            .ReturnsAsync(new ElementHandleBoundingBoxResult { X = 1, Y = 1, Width = 2, Height = 2 });
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .ReturnsAsync(CreatePngImage());
        page.Setup(p => p.QuerySelectorAllAsync("div"))
            .ReturnsAsync(new[] { element.Object });

        await HtmlBrowser.CaptureScreenshotAsync(
            page.Object,
            file,
            new ScreenshotOptions {
                HighlightSelectors = new[] { "div" },
                OverlayText = "test"
            });

        Assert.True(File.Exists(file));
        byte[] original = CreatePngImage();
        byte[] saved = File.ReadAllBytes(file);
        Assert.NotEqual(original, saved);
        var decoded = RasterImageDecoder.Decode(saved);
        var highlightPixel = PixelAt(decoded, 1, 1);
        Assert.True(highlightPixel.R > 180 && highlightPixel.G < 80 && highlightPixel.B < 80);
        element.Verify(e => e.BoundingBoxAsync(), Times.AtLeastOnce());

        File.Delete(file);
        Directory.Delete(dir);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_SavesRequestedFormat() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "test.bmp");
        var page = new Mock<IPage>();
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .ReturnsAsync(CreatePngImage());

        await HtmlBrowser.CaptureScreenshotAsync(
            page.Object,
            file,
            new ScreenshotOptions { Format = ImageFormat.Bmp });

        Assert.True(File.Exists(file));
        using (var stream = File.OpenRead(file)) {
            byte b1 = (byte)stream.ReadByte();
            byte b2 = (byte)stream.ReadByte();
            // BMP header starts with 'B' 'M'
            Assert.Equal((byte)'B', b1);
            Assert.Equal((byte)'M', b2);
        }

        File.Delete(file);
        Directory.Delete(dir);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(50, 5)]
    [InlineData(0, 9)]
    public void QualityToCompression_MapsScreenshotQuality(int quality, int expected) {
        MethodInfo method = typeof(HtmlBrowser).GetMethod("QualityToCompression", BindingFlags.NonPublic | BindingFlags.Static)!;
        int compression = (int)method.Invoke(null, new object[] { quality })!;
        Assert.Equal(expected, compression);
    }

    private static (byte R, byte G, byte B, byte A) PixelAt(RgbaImage image, int x, int y) {
        int offset = (y * image.Width + x) * 4;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2], image.Pixels[offset + 3]);
    }
}
