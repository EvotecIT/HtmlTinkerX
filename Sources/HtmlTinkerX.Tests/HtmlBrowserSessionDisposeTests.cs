using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserSessionDisposeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistentRecordingSavesVideoWhenClosingTheLastPageDisposesItsArtifact(bool closePageOnDispose) {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "htmltinkerx-video-last-page-" + Guid.NewGuid().ToString("N"));
        string output = System.IO.Path.Combine(directory, "recording.webm");
        System.IO.Directory.CreateDirectory(directory);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool pageClosed = false, saveStarted = false;
        var page = new Mock<IPage>();
        page.SetupGet(value => value.IsClosed).Returns(() => pageClosed);
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(() => {
            pageClosed = true;
            if (saveStarted) {
                System.IO.File.WriteAllText(output, "finalized recording");
                finished.TrySetResult(true);
            }
            return Task.CompletedTask;
        });
        var video = new Mock<IVideo>();
        video.Setup(value => value.SaveAsAsync(output)).Returns(() => {
            if (pageClosed) return Task.FromException(new PlaywrightException("Target page, context or browser has been closed"));
            saveStarted = true;
            return finished.Task;
        });
        video.Setup(value => value.PathAsync()).ReturnsAsync(output);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
        var playwright = new Mock<IPlaywright>();
        var session = new HtmlBrowserSession(playwright.Object, null, context.Object, page.Object, video.Object, output,
            userDataDirectory: System.IO.Path.Combine(directory, "profile"), closeBrowserOnDispose: false,
            closePageOnDispose: closePageOnDispose);
        try {
            Task dispose = session.DisposeAsync().AsTask();
            Assert.Same(dispose, await Task.WhenAny(dispose, Task.Delay(5000)));
            await dispose;
            Assert.Equal("finalized recording", System.IO.File.ReadAllText(output));
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            finished.TrySetCanceled();
            if (System.IO.File.Exists(output)) System.IO.File.Delete(output);
            System.IO.Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task PersistentRecordingAttemptsSaveBeforeContextCloseAfterPageCloseFailure(bool pageClosedDespiteError, bool contextFails) {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "htmltinkerx-video-close-" + Guid.NewGuid().ToString("N"));
        string output = System.IO.Path.Combine(directory, "recording.webm");
        System.IO.Directory.CreateDirectory(directory);
        var pageFailure = new PlaywrightException("Page close failed.");
        var contextFailure = new PlaywrightException("Context close failed.");
        var videoFailure = new PlaywrightException("Connection disposed.");
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool pageClosed = false, saveStarted = false, contextCloseStarted = false;
        var page = new Mock<IPage>();
        page.SetupGet(value => value.IsClosed).Returns(() => pageClosed);
        page.Setup(value => value.CloseAsync(It.IsAny<PageCloseOptions>()))
            .Callback(() => {
                pageClosed = pageClosedDespiteError;
                if (pageClosed && saveStarted) {
                    System.IO.File.WriteAllText(output, "finalized recording");
                    finished.TrySetResult(true);
                }
            }).ThrowsAsync(pageFailure);
        var video = new Mock<IVideo>();
        video.Setup(value => value.SaveAsAsync(output)).Returns(() => {
            Assert.False(contextCloseStarted);
            saveStarted = true;
            if (pageClosed) {
                System.IO.File.WriteAllText(output, "finalized recording");
                return Task.CompletedTask;
            }
            return finished.Task;
        });
        video.Setup(value => value.PathAsync()).ReturnsAsync(output);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(() => {
            contextCloseStarted = true;
            if (contextFails) return Task.FromException(contextFailure);
            pageClosed = true;
            if (saveStarted) System.IO.File.WriteAllText(output, "finalized recording");
            finished.TrySetResult(true);
            return Task.CompletedTask;
        });
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(value => value.Dispose()).Callback(() => {
            if (contextFails && saveStarted) finished.TrySetException(videoFailure);
        });
        var session = new HtmlBrowserSession(playwright.Object, null, context.Object, page.Object, video.Object, output,
            userDataDirectory: System.IO.Path.Combine(directory, "profile"), closeBrowserOnDispose: false);
        try {
            Task dispose = session.DisposeAsync().AsTask();
            Assert.Same(dispose, await Task.WhenAny(dispose, Task.Delay(5000)));
            Exception? failure = await Record.ExceptionAsync(() => dispose);
            Assert.NotNull(failure);
            if (contextFails) {
                var aggregate = Assert.IsType<AggregateException>(failure);
                Assert.Contains(pageFailure, aggregate.InnerExceptions);
                Assert.Contains(contextFailure, aggregate.InnerExceptions);
                Assert.Contains(videoFailure, aggregate.InnerExceptions);
            } else {
                Assert.Same(pageFailure, failure);
                Assert.Equal("finalized recording", System.IO.File.ReadAllText(output));
            }
            video.Verify(value => value.SaveAsAsync(output), Times.Once);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
            playwright.Verify(value => value.Dispose(), Times.Once);
        } finally {
            finished.TrySetCanceled();
            if (System.IO.File.Exists(output)) System.IO.File.Delete(output);
            System.IO.Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task DisposeAsync_AllowsNullProperties() {
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllPlaywrightObjects() {
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(p => p.Dispose()).Verifiable();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        await session.DisposeAsync();

        playwright.Verify();
        browser.Verify();
        context.Verify();
    }

    [Fact]
    public async Task DisposeAsync_ReusesCleanupAcrossRepeatedAndConcurrentCalls() {
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(p => p.Dispose()).Verifiable();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var contextCloseStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowContextClose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(async () => {
            contextCloseStarted.SetResult(true);
            await allowContextClose.Task;
        }).Verifiable();
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        Task firstDispose = session.DisposeAsync().AsTask();
        await contextCloseStarted.Task;
        Task concurrentDispose = session.DisposeAsync().AsTask();
        Assert.Same(firstDispose, concurrentDispose);
        allowContextClose.SetResult(true);
        await Task.WhenAll(firstDispose, concurrentDispose);
        await session.DisposeAsync();

        playwright.Verify(p => p.Dispose(), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_RetriesCleanupAfterFailure() {
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(p => p.Dispose()).Verifiable();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var context = new Mock<IBrowserContext>();
        context.SetupSequence(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()))
            .ThrowsAsync(new InvalidOperationException("cleanup failed"))
            .Returns(Task.CompletedTask);
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());
        // One failing owner must not prevent release of the remaining owned resources.
        playwright.Verify(p => p.Dispose(), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
        await session.DisposeAsync();

        playwright.Verify(p => p.Dispose(), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisposeAsync_ReleasesDriverWhenVideoOrBrowserCleanupFails(bool videoFails) {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()))
            .Returns(videoFails ? Task.CompletedTask : Task.FromException(new InvalidOperationException("browser failed")));
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask);
        var video = new Mock<IVideo>();
        video.Setup(v => v.SaveAsAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("video failed"));
        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, new Mock<IPage>().Object,
            videoFails ? video.Object : null, videoFails ? "video.webm" : null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());
        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
        playwright.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_SavesAndCleansVideo() {
        string outFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString(), "video.webm");
        string tempVideo = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempVideo, "tmp");

        var video = new Moq.Mock<IVideo>();
        video.Setup(v => v.SaveAsAsync(It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();
        video.Setup(v => v.PathAsync()).ReturnsAsync(tempVideo);

        var playwright = new Moq.Mock<IPlaywright>();
        var browser = new Moq.Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var context = new Moq.Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var page = new Moq.Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object, video.Object, outFile);

        await session.DisposeAsync();

        video.Verify(v => v.SaveAsAsync(outFile.ToFullPath()), Moq.Times.Once);
        Assert.False(System.IO.File.Exists(tempVideo));
        browser.Verify();
        context.Verify();
    }
}
