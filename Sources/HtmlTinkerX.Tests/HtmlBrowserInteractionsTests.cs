using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserInteractionsTests {
    [Fact]
    public async Task ApplyPageInteractionsAsync_CanceledWhileWaitingForClickTarget_Throws() {
        var page = new Mock<IPage>();
        var locator = new Mock<ILocator>();
        TaskCompletionSource<bool> waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseWait = new(TaskCreationOptions.RunContinuationsAsynchronously);

        locator.SetupGet(l => l.First).Returns(locator.Object);
        locator.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>()))
            .Returns(async () => {
                waitStarted.TrySetResult(true);
                await releaseWait.Task.ConfigureAwait(false);
            });
        page.Setup(p => p.Locator("#slow", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);

        using CancellationTokenSource cts = new();
        Task<IReadOnlyList<string>> interactionTask = HtmlBrowser.ApplyPageInteractionsAsync(
            page.Object,
            clickSelectors: new[] { "#slow" },
            timeout: 30000,
            cancellationToken: cts.Token);

        await WaitForSignalAsync(waitStarted.Task);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interactionTask);
        releaseWait.TrySetResult(true);
    }

    private static async Task WaitForSignalAsync(Task signal) {
        Task completed = await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (completed != signal) {
            throw new TimeoutException("The interaction wait did not start.");
        }
    }
}
