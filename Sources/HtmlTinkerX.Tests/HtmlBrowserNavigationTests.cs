using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
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

    private static async Task WaitForSignalAsync(Task signal) {
        Task completed = await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (completed != signal) {
            throw new TimeoutException("The selector wait did not start.");
        }
    }
}
