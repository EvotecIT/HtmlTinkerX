using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserPdfExportTests {
    [Fact]
    public async Task GetPagePdfAsync_ReturnsBytes() {
        var page = new Mock<IPage>();
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .ReturnsAsync(new byte[] { 1, 2 });

        byte[] data = await HtmlBrowser.GetPagePdfAsync(page.Object);

        Assert.Equal(new byte[] { 1, 2 }, data);
    }

    [Fact]
    public async Task GetPagePdfAsync_SetsFormatOption() {
        PagePdfOptions? options = null;
        var page = new Mock<IPage>();
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>()))
            .Callback<PagePdfOptions>(o => options = o)
            .ReturnsAsync(Array.Empty<byte>());

        await HtmlBrowser.GetPagePdfAsync(page.Object, format: PdfPageFormat.A4);

        Assert.NotNull(options);
        Assert.Equal("A4", options!.Format);
    }

    [Fact]
    public async Task GetPagePdfAsync_CancellationClosesPageToAbortActiveChromiumPrint() {
        TaskCompletionSource<byte[]> pendingPdf = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        page.SetupGet(p => p.IsClosed).Returns(false);
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>())).Returns(pendingPdf.Task);
        page.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(Task.CompletedTask);
        using CancellationTokenSource cancellation = new();

        Task<byte[]> capture = HtmlBrowser.GetPagePdfAsync(page.Object, cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        page.Verify(p => p.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
    }

    [Fact]
    public async Task MaskCleanupDoesNotReplaceActivePdfCancellation() {
        TaskCompletionSource<byte[]> pendingPdf = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool closed = false;
        var page = new Mock<IPage>();
        page.SetupGet(p => p.IsClosed).Returns(() => closed);
        page.Setup(p => p.EvaluateAsync(It.IsAny<string>(), It.IsAny<object?>()))
            .Returns<string, object?>((script, _) => script.Contains("querySelectorAll('[' + marker + ']')", StringComparison.Ordinal)
                ? Task.FromException<JsonElement?>(new PlaywrightException("Target page has been closed"))
                : Task.FromResult<JsonElement?>(null));
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>())).Returns(pendingPdf.Task);
        page.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>()))
            .Callback(() => closed = true)
            .Returns(Task.CompletedTask);
        using CancellationTokenSource cancellation = new();

        Task<byte[]> capture = HtmlBrowser.GetPagePdfAsync(
            page.Object,
            cancellationToken: cancellation.Token,
            maskSelectors: new[] { "#secret" });
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
    }
}
