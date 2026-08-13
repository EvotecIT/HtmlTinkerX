using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserPopupHeaderCoordinatorTests {
    [Fact]
    public async Task PendingAttachmentBlocksReadinessDrainUntilCompletionOrCancellation() {
        TaskCompletionSource<ICDPSession> pendingSession = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryPage = new Mock<IPage>();
        var popupPage = new Mock<IPage>();
        popupPage.SetupGet(value => value.IsClosed).Returns(false);
        popupPage.SetupGet(value => value.Url).Returns("about:blank");
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(popupPage.Object)).Returns(pendingSession.Task);
        HtmlBrowserPopupHeaderCoordinator coordinator = new(
            context.Object,
            primaryPage.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" },
            CancellationToken.None,
            () => { });
        using CancellationTokenSource cancellation = new();

        context.Raise(value => value.Page += null, context.Object, popupPage.Object);
        Task drain = coordinator.WaitForPendingAsync(cancellation.Token);
        Assert.False(drain.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => drain);
        pendingSession.SetException(new PlaywrightException("attachment stopped after cancellation"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task DisposePropagatesAttachmentFailureThatCompletesDuringDrain() {
        TaskCompletionSource<ICDPSession> pendingSession = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryPage = new Mock<IPage>();
        var popupPage = new Mock<IPage>();
        popupPage.SetupGet(value => value.IsClosed).Returns(false);
        popupPage.SetupGet(value => value.Url).Returns("about:blank");
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(popupPage.Object)).Returns(pendingSession.Task);
        HtmlBrowserPopupHeaderCoordinator coordinator = new(
            context.Object,
            primaryPage.Object,
            new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" },
            CancellationToken.None,
            () => { });

        context.Raise(value => value.Page += null, context.Object, popupPage.Object);
        Task disposal = coordinator.DisposeAsync().AsTask();
        pendingSession.SetException(new PlaywrightException("late attachment failed"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => disposal);
        Assert.Contains("late attachment failed", exception.InnerException?.Message);
    }
}
