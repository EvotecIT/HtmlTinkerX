using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlCrawlerDocumentCaptureTests {
    [Theory]
    [InlineData(HtmlCrawlHiddenContentMode.RespectHidden)]
    [InlineData(HtmlCrawlHiddenContentMode.IncludeHidden)]
    public async Task CrawlAsync_DocumentCaptureRecoversWhenNavigationDestroysTheExecutionContext(HtmlCrawlHiddenContentMode hiddenMode) {
        using var fixture = new CaptureFixture();
        fixture.Page.SetupSequence(page => page.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"))
            .ReturnsAsync(fixture.Snapshot);
        fixture.Options.HiddenContentMode = hiddenMode;

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options)).Pages);

        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Contains("Final document", page.Text);
        Assert.Equal(CaptureFixture.Url, page.ResponseUrl);
        Assert.Equal("\"final\"", page.EntityTag);
    }

    [Fact]
    public async Task CrawlAsync_DocumentCaptureSkipsANonHttpReplacementWithoutEvaluatingIt() {
        using var fixture = new CaptureFixture();
        fixture.Options.Timeout = 100;
        fixture.Page.Setup(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .Callback(() => fixture.Page.SetupGet(value => value.Url).Returns("about:blank"))
            .ThrowsAsync(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"));

        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options);

        Assert.Empty(result.Pages);
        HtmlCrawlPage page = Assert.Single(result.SkippedPages);
        Assert.Equal(HtmlCrawlSkipReason.InvalidUrl, page.SkipReason);
        Assert.Equal("about:blank", page.Url);
        Assert.Null(page.ResponseUrl);
        Assert.Null(page.EntityTag);
        Assert.Null(page.LastModified);
        Assert.Null(page.StatusCode);
        Assert.Null(page.ContentType);
        fixture.Page.Verify(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CrawlAsync_DocumentCaptureDoesNotRetryAnUnrelatedScriptFailure() {
        using var fixture = new CaptureFixture();
        fixture.Page.Setup(page => page.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new PlaywrightException("The document script failed"));

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Equal("The document script failed", page.Error);
        fixture.Page.Verify(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CrawlAsync_DocumentCaptureTimeoutIncludesWaitingForTheReplacementDocument() {
        using var fixture = new CaptureFixture();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Options.Timeout = 100;
        fixture.Page.Setup(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"));
        fixture.Page.Setup(value => value.WaitForLoadStateAsync(It.IsAny<LoadState?>(), It.IsAny<PageWaitForLoadStateOptions>())).Returns(pending.Task);
        try {
            HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options)).Pages);
            Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
            Assert.Equal("Rendered document capture exceeded its 100 ms timeout.", page.Error);
        } finally {
            pending.TrySetResult(true);
        }
    }

    [Fact]
    public async Task CrawlAsync_DocumentCaptureCancellationStopsWaitingForTheReplacementDocument() {
        using var fixture = new CaptureFixture();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        fixture.Options.Timeout = 2000;
        fixture.Page.Setup(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"));
        fixture.Page.Setup(value => value.WaitForLoadStateAsync(It.IsAny<LoadState?>(), It.IsAny<PageWaitForLoadStateOptions>()))
            .Callback(() => entered.TrySetResult(true)).Returns(pending.Task);
        try {
            Task<HtmlCrawlResult> crawl = HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options, cancellation.Token);
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(1000)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => crawl);
        } finally {
            pending.TrySetResult(true);
        }
    }

    [Fact]
    public async Task CrawlAsync_DocumentCaptureDoesNotRetryAfterThePageCloses() {
        using var fixture = new CaptureFixture();
        fixture.Page.SetupGet(value => value.IsClosed).Returns(true);
        fixture.Page.Setup(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"));

        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(CaptureFixture.Url, fixture.Options)).Pages);

        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        fixture.Page.Verify(value => value.EvaluateAsync<JsonElement>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    private sealed class CaptureFixture : IDisposable {
        internal const string Url = "https://capture.test/";
        private readonly Func<Task<IPlaywright>>? _originalFactory = HtmlBrowser.PlaywrightFactory;
        internal Mock<IPage> Page { get; } = new();
        internal JsonElement Snapshot { get; } = JsonDocument.Parse("{\"url\":\"https://capture.test/\",\"title\":\"Final\",\"documentUrl\":\"https://capture.test/\",\"html\":\"<main>Final document</main>\"}").RootElement.Clone();
        internal HtmlCrawlOptions Options { get; } = new() {
            Render = true, Browser = HtmlBrowserEngine.Firefox, MaxPages = 1,
            RespectRobotsTxt = false, UseSitemaps = false, AutoProfile = false,
            IncludeMarkdown = false, Timeout = 1000
        };

        internal CaptureFixture() {
            var response = new Mock<IResponse>();
            response.SetupGet(value => value.Url).Returns(Url);
            response.SetupGet(value => value.Status).Returns(200);
            response.SetupGet(value => value.Ok).Returns(true);
            response.SetupGet(value => value.Headers).Returns(new Dictionary<string, string> {
                ["content-type"] = "text/html", ["etag"] = "\"final\""
            });
            Page.SetupGet(value => value.Url).Returns(Url);
            Page.Setup(value => value.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>())).ReturnsAsync(response.Object);
            Page.Setup(value => value.EvaluateAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync((JsonElement?)null);
            Page.Setup(value => value.WaitForLoadStateAsync(It.IsAny<LoadState?>(), It.IsAny<PageWaitForLoadStateOptions>())).Returns(Task.CompletedTask);
            var context = new Mock<IBrowserContext>();
            context.Setup(value => value.NewPageAsync()).ReturnsAsync(Page.Object);
            var browser = new Mock<IBrowser>();
            browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
            var browserType = new Mock<IBrowserType>();
            browserType.Setup(value => value.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
            var playwright = new Mock<IPlaywright>();
            playwright.SetupGet(value => value.Firefox).Returns(browserType.Object);
            HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        }

        public void Dispose() => HtmlBrowser.PlaywrightFactory = _originalFactory;
    }
}
