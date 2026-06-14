using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserNavigationTests {
    [Fact]
    public async Task PreparePageForContentAsync_CanceledWhileWaitingForSelector_Throws() {
        var page = new Mock<IPage>();
        TaskCompletionSource<bool> waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseWait = new(TaskCreationOptions.RunContinuationsAsynchronously);

        page.Setup(p => p.WaitForSelectorAsync("#slow", It.IsAny<PageWaitForSelectorOptions?>()))
            .Returns(async () => {
                waitStarted.TrySetResult(true);
                await releaseWait.Task.ConfigureAwait(false);
                return null;
            });

        using CancellationTokenSource cts = new();
        Task waitTask = HtmlBrowser.PreparePageForContentAsync(
            page.Object,
            waitForSelector: "#slow",
            timeout: 30000,
            cancellationToken: cts.Token);

        await WaitForSignalAsync(waitStarted.Task);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
        releaseWait.TrySetResult(true);
    }

    [Fact]
    public async Task GetContentAsync_CanceledWhileWaitingForSelector_Throws() {
        var page = new Mock<IPage>();
        var locator = new Mock<ILocator>();
        TaskCompletionSource<bool> waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseWait = new(TaskCreationOptions.RunContinuationsAsynchronously);

        locator.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>()))
            .Returns(async () => {
                waitStarted.TrySetResult(true);
                await releaseWait.Task.ConfigureAwait(false);
            });
        page.Setup(p => p.Locator("#slow", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);

        using CancellationTokenSource cts = new();
        Task<string> contentTask = HtmlBrowser.GetContentAsync(
            page.Object,
            "#slow",
            innerHtml: false,
            asText: false,
            timeout: 30000,
            cancellationToken: cts.Token);

        await WaitForSignalAsync(waitStarted.Task);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => contentTask);
        releaseWait.TrySetResult(true);
    }

    [Fact]
    public async Task PreparePageForContentAsync_CanceledDuringAutoScrollDelay_Throws() {
        var page = new Mock<IPage>();
        TaskCompletionSource<bool> waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseWait = new(TaskCreationOptions.RunContinuationsAsynchronously);

        page.Setup(p => p.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)", null))
            .Returns(Task.FromResult<JsonElement?>(null));
        page.Setup(p => p.WaitForTimeoutAsync(It.IsAny<float>()))
            .Returns(async () => {
                waitStarted.TrySetResult(true);
                await releaseWait.Task.ConfigureAwait(false);
            });

        using CancellationTokenSource cts = new();
        Task waitTask = HtmlBrowser.PreparePageForContentAsync(
            page.Object,
            autoScroll: true,
            autoScrollSteps: 1,
            autoScrollDelayMs: 30000,
            cancellationToken: cts.Token);

        await WaitForSignalAsync(waitStarted.Task);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
        releaseWait.TrySetResult(true);
    }

    private static async Task WaitForSignalAsync(Task signal) {
        Task completed = await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (completed != signal) {
            throw new TimeoutException("The selector wait did not start.");
        }
    }
}
