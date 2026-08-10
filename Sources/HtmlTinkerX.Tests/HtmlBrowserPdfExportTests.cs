using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
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

        await HtmlBrowser.GetPagePdfAsync(page.Object, new HtmlBrowserPdfOptions(format: PdfPageFormat.A4));

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
    public async Task GetPagePdfAsync_PreCancelledTokenDoesNotStartChromiumPrint() {
        var page = new Mock<IPage>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlBrowser.GetPagePdfAsync(page.Object, cancellationToken: cancellation.Token));

        page.Verify(p => p.PdfAsync(It.IsAny<PagePdfOptions>()), Times.Never);
        page.Verify(p => p.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Never);
    }

    [Fact]
    public async Task GetPagePdfAsync_CancellationDoesNotWaitForWedgedPageClose() {
        TaskCompletionSource<byte[]> pendingPdf = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> pendingClose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        page.SetupGet(p => p.IsClosed).Returns(false);
        page.Setup(p => p.PdfAsync(It.IsAny<PagePdfOptions>())).Returns(pendingPdf.Task);
        page.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(pendingClose.Task);
        using CancellationTokenSource cancellation = new();

        Task<byte[]> capture = HtmlBrowser.GetPagePdfAsync(page.Object, cancellationToken: cancellation.Token);
        cancellation.Cancel();

        Assert.Same(capture, await Task.WhenAny(capture, Task.Delay(TimeSpan.FromSeconds(2))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
    }

    [Fact]
    public async Task StableMarkupReadHonorsItsReadinessDeadline() {
        TaskCompletionSource<string> pendingContent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> pendingClose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        page.Setup(value => value.ContentAsync()).Returns(pendingContent.Task);
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(pendingClose.Task);
        HtmlBrowserPdfReadiness readiness = new(skipLoadState: true, stable: true, timeout: 100);

        Task wait = HtmlBrowserPdfCapture.WaitForReadinessAsync(page.Object, readiness, CancellationToken.None);

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(2))));
        await Assert.ThrowsAsync<TimeoutException>(() => wait);
    }

    [Fact]
    public async Task LaunchBrowserAsync_DisposesPlaywrightWhenLaunchFails() {
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(type => type.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ThrowsAsync(new PlaywrightException("launch failed"));
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        try {
            await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowser.LaunchBrowserAsync(new HtmlBrowserLaunchOptions(), CancellationToken.None));
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
        }
    }

    [Fact]
    public async Task OpenSessionAsync_CleansAllOwnersWhenSetupFailsAfterLaunch() {
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewPageAsync()).ReturnsAsync(page.Object);
        context.Setup(value => value.AddInitScriptAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new PlaywrightException("init failed"));
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
        var browser = new Mock<IBrowser>();
        browser.SetupGet(value => value.IsConnected).Returns(true);
        browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        browser.Setup(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(value => value.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        HtmlBrowserLaunchOptions options = new();
        options.InitScripts.Add("window.__setup = true;");
        try {
            await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowser.OpenSessionAsync("https://example.com", options));
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.Once);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
        }
    }

    [Fact]
    public async Task OpenSessionAsync_CdpCancellationDoesNotCloseExternalBrowser() {
        TaskCompletionSource<IBrowserContext> pendingContext = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var browser = new Mock<IBrowser>();
        browser.SetupGet(value => value.Contexts).Returns(Array.Empty<IBrowserContext>());
        browser.SetupGet(value => value.IsConnected).Returns(true);
        browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).Returns(pendingContext.Task);
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(value => value.ConnectOverCDPAsync(It.IsAny<string>(), It.IsAny<BrowserTypeConnectOverCDPOptions>()))
            .ReturnsAsync(browser.Object);
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        using CancellationTokenSource cancellation = new();
        HtmlBrowserLaunchOptions options = new() { CdpEndpointUrl = "http://127.0.0.1:9222" };
        try {
            Task<HtmlBrowserSession> opening = HtmlBrowser.OpenSessionAsync("https://example.com", options, cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.Never);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
        }
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
            new HtmlBrowserPdfOptions(maskSelectors: new[] { "#secret" }),
            cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
    }
}
