using Microsoft.Playwright;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task ContextCreationDeadlineAbortsAndRecyclesTheBrowserSlot() {
        TaskCompletionSource<IBrowserContext> pendingContext = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var browser = new Mock<IBrowser>();
        browser.SetupGet(value => value.IsConnected).Returns(true);
        browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).Returns(pendingContext.Task);
        browser.Setup(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(value => value.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        try {
            await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
                maximumBrowserInstances: 1,
                contextCreationTimeout: TimeSpan.FromMilliseconds(25),
                networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => renderer.CaptureAsync(
                new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromHtml("<p>deadline</p>"))));

            Assert.Contains("context creation", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, renderer.GetMetricsSnapshot().BrowsersRecycled);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.AtLeastOnce);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
        }
    }

    [Fact]
    public async Task CallerScriptFailureThatResemblesATransportErrorDoesNotReplayCapture() {
        await using LoopbackContentServer origin = new("<html><body><p>run once</p></body></html>");
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        await Assert.ThrowsAsync<PlaywrightException>(() => renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(origin.Url),
            beforeCaptureScript: "throw new Error('target page, context or browser has been closed');")));

        HtmlBrowserPdfRendererMetrics failed = renderer.GetMetricsSnapshot();
        Assert.Equal(1, origin.RequestCount);
        Assert.Equal(0, failed.BrowserFailureRetries);
        Assert.Equal(0, failed.BrowsersRecycled);
        Assert.Equal(1, failed.BrowsersCreated);

        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>healthy browser reused</p></body></html>")));

        AssertPdfContains(recovered.PdfBytes, "healthy browser reused");
        Assert.True(recovered.Diagnostics.BrowserReused);
        Assert.Equal(1, renderer.GetMetricsSnapshot().BrowsersCreated);
    }
}
