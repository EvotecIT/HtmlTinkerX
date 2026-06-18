using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests file saving operations performed by <see cref="HtmlBrowser"/>.
/// </summary>
public class HtmlBrowserFileSavingTests {
    [Fact]
    public async Task CaptureScreenshotAsync_CreatesDirectoryAndFile() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "test.png");
        var page = new Mock<IPage>();
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        await HtmlBrowser.CaptureScreenshotAsync(page.Object, file);

        Assert.True(Directory.Exists(dir));
        Assert.True(File.Exists(file));
        byte[] data = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 1, 2, 3 }, data);

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

    [Fact]
    public async Task SavePagePdfAsync_PassesOptionsToPlaywright() {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "test.pdf");
        PagePdfOptions? options = null;
        var page = new Mock<IPage>();
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .Callback<PagePdfOptions>(o => options = o)
            .ReturnsAsync(Array.Empty<byte>());

        await HtmlBrowser.SavePagePdfAsync(
            page.Object,
            file,
            landscape: true,
            marginTop: "1cm",
            marginRight: "2cm",
            marginBottom: "3cm",
            marginLeft: "4cm");

        Assert.NotNull(options);
        Assert.Equal(file, options!.Path);
        Assert.True(options.Landscape);
        Assert.NotNull(options.Margin);
        Assert.Equal("1cm", options.Margin!.Top);
        Assert.Equal("2cm", options.Margin!.Right);
        Assert.Equal("3cm", options.Margin!.Bottom);
        Assert.Equal("4cm", options.Margin!.Left);
    }

    [Fact]
    public async Task SavePagePdfAsync_SetsFormatOption() {
        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        PagePdfOptions? options = null;
        var page = new Mock<IPage>();
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .Callback<PagePdfOptions>(o => options = o)
            .ReturnsAsync(Array.Empty<byte>());

        await HtmlBrowser.SavePagePdfAsync(page.Object, file, format: PdfPageFormat.A4);

        Assert.NotNull(options);
        Assert.Equal("A4", options!.Format);
    }

    [Fact]
    public async Task SavePagePdfAsync_MasksAndRestoresVisualElements() {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "test.pdf");
        var operations = new List<string>();
        var evaluateArguments = new List<object?>();
        var page = new Mock<IPage>();
        page.Setup(p => p.EvaluateAsync(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback<string, object?>((script, argument) => {
                operations.Add(script.Contains("querySelectorAll('[' + marker + ']')", StringComparison.Ordinal) ? "restore" : "mask");
                evaluateArguments.Add(argument);
            })
            .ReturnsAsync((JsonElement?)default);
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .Callback<PagePdfOptions>(o => {
                operations.Add("pdf");
                File.WriteAllText(o.Path!, "pdf");
            })
            .ReturnsAsync(Array.Empty<byte>());

        await HtmlBrowser.SavePagePdfAsync(
            page.Object,
            file,
            maskSensitiveElements: true,
            maskSelectors: new[] { "#token" },
            maskColor: "#00ff00");

        Assert.Equal(new[] { "mask", "pdf", "restore" }, operations);
        string maskArgumentJson = JsonSerializer.Serialize(evaluateArguments[0]);
        Assert.Contains("#token", maskArgumentJson, StringComparison.Ordinal);
        Assert.Contains("#00ff00", maskArgumentJson, StringComparison.Ordinal);

        Directory.Delete(Path.GetDirectoryName(file)!, true);
    }

    [Fact]
    public async Task SavePagePdfAsync_NegativeDelayThrows() {
        var page = new Mock<IPage>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await HtmlBrowser.SavePagePdfAsync(page.Object, "file.pdf", delayMs: -1));
    }
}
