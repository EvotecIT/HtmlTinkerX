using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Microsoft.Playwright;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserFileSavingTests {
    [Fact]
    public async Task CaptureScreenshotAsync_CreatesDirectoryAndFile() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "test.png");
        var page = new Mock<IPage>();
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .ReturnsAsync(new byte[] {1,2,3});

        await HtmlBrowser.CaptureScreenshotAsync(page.Object, file);

        Assert.True(Directory.Exists(dir));
        Assert.True(File.Exists(file));
        byte[] data = File.ReadAllBytes(file);
        Assert.Equal(new byte[] {1,2,3}, data);

        File.Delete(file);
        Directory.Delete(dir);
    }

    [Fact]
    public async Task SavePagePdfAsync_CreatesDirectoryAndFile() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "test.pdf");
        var page = new Mock<IPage>();
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .Callback<PagePdfOptions>(o => File.WriteAllText(o.Path!, "pdf"))
            .ReturnsAsync(Array.Empty<byte>());

        await HtmlBrowser.SavePagePdfAsync(page.Object, file);

        Assert.True(Directory.Exists(dir));
        Assert.True(File.Exists(file));
        string content = File.ReadAllText(file);
        Assert.Equal("pdf", content);

        File.Delete(file);
        Directory.Delete(dir);
    }
}
