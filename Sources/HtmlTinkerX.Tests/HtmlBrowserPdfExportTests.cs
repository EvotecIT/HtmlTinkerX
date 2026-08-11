using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserPdfExportTests {
    [Fact]
    public async Task ScopedHeaderInterceptorBoundsItsEntireTeardown() {
        TaskCompletionSource<JsonElement?> pendingCommand = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> cleanupTimedOut = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(pendingCommand.Task);
        HtmlBrowserScopedHeaderInterceptor interceptor = new(
            session.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" },
            TimeSpan.FromMilliseconds(25),
            () => cleanupTimedOut.TrySetResult(true));

        await interceptor.DisposeAsync();

        Assert.Equal(TaskStatus.RanToCompletion, cleanupTimedOut.Task.Status);
        Assert.False(pendingCommand.Task.IsCompleted);
    }

    [Fact]
    public async Task ScopedHeaderInterceptorPropagatesWorkerConfigurationFailure() {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events[name] = value;
            }
            return value;
        }
        TaskCompletionSource<bool> interceptionFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>()))
            .Returns<string>(name => Event(name).Object);
        string? autoAttachMessage = null;
        string? resumeMessage = null;
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => {
                if (method == "Target.sendMessageToTarget" && arguments != null) {
                    string sentMessage = arguments["message"].ToString()!;
                    if (sentMessage.Contains("\"method\":\"Target.setAutoAttach\"", StringComparison.Ordinal)) autoAttachMessage = sentMessage;
                    if (sentMessage.Contains("\"method\":\"Runtime.runIfWaitingForDebugger\"", StringComparison.Ordinal)) resumeMessage = sentMessage;
                }
                return Task.FromResult<JsonElement?>(null);
            });
        session.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(session.Object);
        await using HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            context.Object,
            page.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" },
            CancellationToken.None,
            () => interceptionFailed.TrySetResult(true));
        JsonElement attachedWorker = ParseJson(@"{""sessionId"":""worker-1"",""targetInfo"":{""type"":""worker""}}");

        Event("Target.attachedToTarget").Raise(value => value.OnEvent += null, session.Object, attachedWorker);
        Assert.True(SpinWait.SpinUntil(() => autoAttachMessage != null, TimeSpan.FromSeconds(2)));
        using JsonDocument sent = JsonDocument.Parse(autoAttachMessage!);
        long commandId = sent.RootElement.GetProperty("id").GetInt64();
        string failedResponse = JsonSerializer.Serialize(new Dictionary<string, object> {
            ["sessionId"] = "worker-1",
            ["message"] = JsonSerializer.Serialize(new Dictionary<string, object> {
                ["id"] = commandId,
                ["error"] = new Dictionary<string, object> { ["message"] = "worker interception failed" }
            })
        });
        Event("Target.receivedMessageFromTarget").Raise(value => value.OnEvent += null, session.Object, ParseJson(failedResponse));
        Assert.True(SpinWait.SpinUntil(() => resumeMessage != null, TimeSpan.FromSeconds(2)));
        using JsonDocument resume = JsonDocument.Parse(resumeMessage!);
        string resumedResponse = JsonSerializer.Serialize(new Dictionary<string, object> {
            ["sessionId"] = "worker-1",
            ["message"] = JsonSerializer.Serialize(new Dictionary<string, object> {
                ["id"] = resume.RootElement.GetProperty("id").GetInt64(),
                ["result"] = new Dictionary<string, object>()
            })
        });
        Event("Target.receivedMessageFromTarget").Raise(value => value.OnEvent += null, session.Object, ParseJson(resumedResponse));
        Assert.Same(interceptionFailed.Task, await Task.WhenAny(interceptionFailed.Task, Task.Delay(TimeSpan.FromSeconds(2))));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(interceptor.ThrowIfFaulted);
        Assert.Contains("worker interception failed", exception.InnerException?.Message);
    }

    [Fact]
    public async Task ScopedHeaderInterceptorIdentifiesOnlyThePrimaryFrameDocumentAsTopLevel() {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events[name] = value;
            }
            return value;
        }
        List<bool> blockedDocuments = new();
        TaskCompletionSource<bool> callbacksCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>()))
            .Returns<string>(name => Event(name).Object);
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) =>
                Task.FromResult<JsonElement?>(method == "Page.getFrameTree"
                    ? ParseJson(@"{""frameTree"":{""frame"":{""id"":""main-frame""}}}")
                    : null));
        session.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(session.Object);
        await using HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            context.Object,
            page.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string>(),
            CancellationToken.None,
            requestAllowed: _ => Task.FromResult(false),
            requestBlocked: (_, topLevel) => {
                blockedDocuments.Add(topLevel);
                if (blockedDocuments.Count == 2) callbacksCompleted.TrySetResult(true);
            });

        Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
            ParseJson(@"{""requestId"":""child"",""frameId"":""child-frame"",""resourceType"":""Document"",""request"":{""url"":""https://blocked.test/child"",""headers"":{}}}"));
        Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
            ParseJson(@"{""requestId"":""main"",""frameId"":""main-frame"",""resourceType"":""Document"",""request"":{""url"":""https://blocked.test/main"",""headers"":{}}}"));
        Assert.Same(callbacksCompleted.Task, await Task.WhenAny(callbacksCompleted.Task, Task.Delay(TimeSpan.FromSeconds(2))));

        Assert.Equal(new[] { false, true }, blockedDocuments);
    }

    [Fact]
    public async Task StorageInitializationBoundsSessionDetach() {
        TaskCompletionSource<bool> pendingDetach = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> cleanupTimedOut = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var contextCreated = new Mock<ICDPSessionEvent>();
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event("Runtime.executionContextCreated")).Returns(contextCreated.Object);
        session.Setup(value => value.DetachAsync()).Returns(pendingDetach.Task);
        HtmlBrowserStorageInitialization initialization = new(
            session.Object,
            "storage-world",
            "storage-status",
            "main-frame",
            () => cleanupTimedOut.TrySetResult(true),
            TimeSpan.FromMilliseconds(25));

        await initialization.DisposeAsync();

        Assert.Equal(TaskStatus.RanToCompletion, cleanupTimedOut.Task.Status);
        Assert.False(pendingDetach.Task.IsCompleted);
    }

    [Fact]
    public async Task PopupCoordinatorPropagatesBackgroundHeaderAttachmentFailure() {
        TaskCompletionSource<bool> attachmentFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryPage = new Mock<IPage>();
        var popupPage = new Mock<IPage>();
        popupPage.SetupGet(value => value.IsClosed).Returns(false);
        popupPage.SetupGet(value => value.Url).Returns("about:blank");
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(popupPage.Object))
            .ThrowsAsync(new PlaywrightException("attachment failed"));
        await using HtmlBrowserPopupHeaderCoordinator coordinator = new(
            context.Object,
            primaryPage.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" },
            CancellationToken.None,
            () => attachmentFailed.TrySetResult(true));

        context.Raise(value => value.Page += null, context.Object, popupPage.Object);
        Assert.Same(attachmentFailed.Task, await Task.WhenAny(attachmentFailed.Task, Task.Delay(TimeSpan.FromSeconds(2))));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(coordinator.ThrowIfFaulted);
        Assert.Contains("attachment failed", exception.InnerException?.Message);
    }

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
        var frame = new Mock<IFrame>();
        frame.SetupGet(value => value.IsDetached).Returns(false);
        frame.SetupGet(value => value.Url).Returns("about:blank");
        frame.Setup(value => value.ContentAsync()).Returns(pendingContent.Task);
        var page = new Mock<IPage>();
        page.SetupGet(value => value.Frames).Returns(new[] { frame.Object });
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(pendingClose.Task);
        HtmlBrowserPdfReadiness readiness = new(skipLoadState: true, stable: true, timeout: 100);

        Task wait = HtmlBrowserPdfCapture.WaitForReadinessAsync(page.Object, readiness, CancellationToken.None);

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(2))));
        await Assert.ThrowsAsync<TimeoutException>(() => wait);
    }

    [Fact]
    public async Task ZeroStabilityTimeoutWaitsWithoutAnInternalDeadline() {
        var frame = new Mock<IFrame>();
        frame.SetupGet(value => value.IsDetached).Returns(false);
        frame.SetupGet(value => value.Url).Returns("about:blank");
        frame.Setup(value => value.ContentAsync()).ReturnsAsync("<main>stable</main>");
        var page = new Mock<IPage>();
        page.SetupGet(value => value.Frames).Returns(new[] { frame.Object });
        HtmlBrowserPdfReadiness readiness = new(skipLoadState: true, stable: true, stableMilliseconds: 0, pollMilliseconds: 1, timeout: 0);

        await HtmlBrowserPdfCapture.WaitForReadinessAsync(page.Object, readiness, CancellationToken.None);

        frame.Verify(value => value.ContentAsync(), Times.AtLeast(2));
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
    public async Task LaunchBrowserAsync_CancellationClosesBrowserThatFinishesLaunchingLate() {
        TaskCompletionSource<IBrowser> pendingLaunch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> launchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> browserClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> ownerDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var browser = new Mock<IBrowser>();
        browser.Setup(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()))
            .Callback(() => browserClosed.TrySetResult(true))
            .Returns(Task.CompletedTask);
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(type => type.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .Callback(() => launchStarted.TrySetResult(true))
            .Returns(pendingLaunch.Task);
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        playwright.Setup(value => value.Dispose()).Callback(() => ownerDisposed.TrySetResult(true));
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        using CancellationTokenSource cancellation = new();
        try {
            Task<(IPlaywright Playwright, IBrowser Browser)> launching =
                HtmlBrowser.LaunchBrowserAsync(new HtmlBrowserLaunchOptions(), cancellation.Token);
            await launchStarted.Task;
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launching);
            pendingLaunch.SetResult(browser.Object);

            Assert.Same(browserClosed.Task, await Task.WhenAny(browserClosed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Same(ownerDisposed.Task, await Task.WhenAny(ownerDisposed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.Once);
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
        TaskCompletionSource<bool> contextClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()))
            .Callback(() => contextClosed.TrySetResult(true))
            .Returns(Task.CompletedTask);
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
            pendingContext.SetResult(context.Object);
            Task completed = await Task.WhenAny(contextClosed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(contextClosed.Task, completed);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.Never);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
        }
    }

    [Fact]
    public async Task CdpContextCleanupDisposesItsOwnerWhenCreationNeverCompletes() {
        TaskCompletionSource<IBrowserContext> pendingContext = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> ownerDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(value => value.Dispose()).Callback(() => ownerDisposed.TrySetResult(true));

        HtmlBrowser.CloseContextWhenCreated(
            pendingContext.Task,
            playwright.Object,
            TimeSpan.FromMilliseconds(25));

        Task completed = await Task.WhenAny(ownerDisposed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(ownerDisposed.Task, completed);
        playwright.Verify(value => value.Dispose(), Times.Once);
        Assert.False(pendingContext.Task.IsCompleted);
    }

    [Fact]
    public async Task CdpContextCleanupBoundsTheClosePhase() {
        TaskCompletionSource<bool> pendingClose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> ownerDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(pendingClose.Task);
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(value => value.Dispose()).Callback(() => ownerDisposed.TrySetResult(true));

        HtmlBrowser.CloseContextWhenCreated(
            Task.FromResult(context.Object),
            playwright.Object,
            TimeSpan.FromMilliseconds(25));

        Assert.Same(ownerDisposed.Task, await Task.WhenAny(ownerDisposed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.False(pendingClose.Task.IsCompleted);
        context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
        playwright.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CdpPageCleanupBoundsTheClosePhase() {
        TaskCompletionSource<bool> pendingClose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> ownerDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(pendingClose.Task);
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(value => value.Dispose()).Callback(() => ownerDisposed.TrySetResult(true));

        HtmlBrowser.ClosePageWhenCreated(
            Task.FromResult(page.Object),
            playwright.Object,
            TimeSpan.FromMilliseconds(25));

        Assert.Same(ownerDisposed.Task, await Task.WhenAny(ownerDisposed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.False(pendingClose.Task.IsCompleted);
        page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
        playwright.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task OpenSessionAsync_CdpRejectsIgnoreHttpsErrorsBecauseExistingContextsCannotBeReconfigured() {
        HtmlBrowserLaunchOptions options = new() {
            CdpEndpointUrl = "http://127.0.0.1:9222",
            IgnoreHTTPSErrors = true
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            HtmlBrowser.OpenSessionAsync("https://example.com", options));

        Assert.Contains("IgnoreHTTPSErrors", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenSessionAsync_CdpCancellationClosesPageCreatedAfterCancellation() {
        TaskCompletionSource<IPage> pendingPage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> pageClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        page.SetupGet(value => value.IsClosed).Returns(false);
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>()))
            .Callback(() => pageClosed.TrySetResult(true))
            .Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewPageAsync()).Returns(pendingPage.Task);
        var browser = new Mock<IBrowser>();
        browser.SetupGet(value => value.Contexts).Returns(new[] { context.Object });
        browser.SetupGet(value => value.IsConnected).Returns(true);
        var browserType = new Mock<IBrowserType>();
        browserType.Setup(value => value.ConnectOverCDPAsync(It.IsAny<string>(), It.IsAny<BrowserTypeConnectOverCDPOptions>())).ReturnsAsync(browser.Object);
        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        using CancellationTokenSource cancellation = new();
        try {
            Task<HtmlBrowserSession> opening = HtmlBrowser.OpenSessionAsync(
                "https://example.com",
                new HtmlBrowserLaunchOptions { CdpEndpointUrl = "http://127.0.0.1:9222" },
                cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            pendingPage.SetResult(page.Object);
            Task completed = await Task.WhenAny(pageClosed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(pageClosed.Task, completed);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Never);
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
        var cdp = new Mock<ICDPSession>();
        cdp.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => method switch {
                "Page.getFrameTree" => Task.FromResult<JsonElement?>(ParseJson(@"{""frameTree"":{""frame"":{""id"":""main""}}}")),
                "Page.createIsolatedWorld" => Task.FromResult<JsonElement?>(ParseJson(@"{""executionContextId"":1}")),
                "Runtime.evaluate" when arguments != null
                    && arguments.TryGetValue("expression", out object? expression)
                    && expression.ToString()!.Contains("delete globalThis[stateKey]", StringComparison.Ordinal) =>
                        Task.FromException<JsonElement?>(new PlaywrightException("Target page has been closed")),
                _ => Task.FromResult<JsonElement?>(null)
            });
        cdp.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(cdp.Object);
        page.SetupGet(value => value.Context).Returns(context.Object);
        page.SetupGet(p => p.IsClosed).Returns(() => closed);
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

    [Fact]
    public async Task MaskApplicationHonorsDirectPdfCancellation() {
        TaskCompletionSource<JsonElement?> pendingMask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cdp = new Mock<ICDPSession>();
        cdp.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, _) => method switch {
                "Page.getFrameTree" => Task.FromResult<JsonElement?>(ParseJson(@"{""frameTree"":{""frame"":{""id"":""main""}}}")),
                "Page.createIsolatedWorld" => Task.FromResult<JsonElement?>(ParseJson(@"{""executionContextId"":1}")),
                "Runtime.evaluate" => pendingMask.Task,
                _ => Task.FromResult<JsonElement?>(null)
            });
        cdp.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(cdp.Object);
        page.SetupGet(value => value.Context).Returns(context.Object);
        page.SetupGet(value => value.IsClosed).Returns(false);
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(Task.CompletedTask);
        using CancellationTokenSource cancellation = new();

        Task<byte[]> capture = HtmlBrowser.GetPagePdfAsync(
            page.Object,
            new HtmlBrowserPdfOptions(maskSelectors: new[] { "#secret" }),
            cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
        page.Verify(value => value.PdfAsync(It.IsAny<PagePdfOptions>()), Times.Never);
    }

    [Fact]
    public async Task MaskApplicationSkipsAFrameThatDetachesAfterTheFrameSnapshot() {
        int frameTreeReads = 0;
        var cdp = new Mock<ICDPSession>();
        cdp.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => method switch {
                "Page.getFrameTree" when Interlocked.Increment(ref frameTreeReads) == 1 =>
                    Task.FromResult<JsonElement?>(ParseJson(@"{""frameTree"":{""frame"":{""id"":""main""},""childFrames"":[{""frame"":{""id"":""detached""}}]}}")),
                "Page.getFrameTree" =>
                    Task.FromResult<JsonElement?>(ParseJson(@"{""frameTree"":{""frame"":{""id"":""main""}}}")),
                "Page.createIsolatedWorld" when arguments != null && (string)arguments["frameId"] == "detached" =>
                    Task.FromException<JsonElement?>(new PlaywrightException("No frame with given id found")),
                "Page.createIsolatedWorld" =>
                    Task.FromResult<JsonElement?>(ParseJson(@"{""executionContextId"":1}")),
                _ => Task.FromResult<JsonElement?>(null)
            });
        cdp.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(cdp.Object);
        page.SetupGet(value => value.Context).Returns(context.Object);

        string result = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            page.Object,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret" },
            maskColor: "#000000",
            action: () => Task.FromResult("captured"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("captured", result);
        Assert.Equal(2, frameTreeReads);
    }

    [Fact]
    public async Task MaskCleanupPropagatesRestorationFailureForALiveFrame() {
        int frameTreeReads = 0;
        var cdp = new Mock<ICDPSession>();
        cdp.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => method switch {
                "Page.getFrameTree" => Task.FromResult<JsonElement?>(ParseJson(@"{""frameTree"":{""frame"":{""id"":""main""}}}")),
                "Page.createIsolatedWorld" => Task.FromResult<JsonElement?>(ParseJson(@"{""executionContextId"":1}")),
                "Runtime.evaluate" when arguments != null
                    && arguments.TryGetValue("expression", out object? expression)
                    && expression.ToString()!.Contains("delete globalThis[stateKey]", StringComparison.Ordinal) =>
                        Task.FromException<JsonElement?>(new PlaywrightException("Transient CDP restore failure")),
                _ => Task.FromResult<JsonElement?>(null)
            })
            .Callback<string, Dictionary<string, object>?>((method, _) => {
                if (method == "Page.getFrameTree") Interlocked.Increment(ref frameTreeReads);
            });
        cdp.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(cdp.Object);
        page.SetupGet(value => value.Context).Returns(context.Object);
        page.SetupGet(value => value.IsClosed).Returns(false);

        PlaywrightException exception = await Assert.ThrowsAsync<PlaywrightException>(() =>
            HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
                page.Object,
                maskSensitiveElements: false,
                maskSelectors: new[] { "#secret" },
                maskColor: "#000000",
                action: () => Task.FromResult(true),
                cancellationToken: CancellationToken.None));

        Assert.Contains("restore failure", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, frameTreeReads);
        cdp.Verify(value => value.DetachAsync(), Times.Once);
    }

    private static JsonElement ParseJson(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
