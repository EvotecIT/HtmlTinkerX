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
public sealed class HtmlBrowserPopupHeaderCoordinatorTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReleaseDoesNotProbeABlankRealmAfterThePopupClosesOrNavigates(bool closePopup) {
        bool closed = false;
        string url = "about:blank";
        var popupPage = new Mock<IPage>();
        popupPage.SetupGet(value => value.IsClosed).Returns(() => closed);
        popupPage.SetupGet(value => value.Url).Returns(() => url);
        popupPage.Setup(value => value.AddInitScriptAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());
        popupPage.Setup(value => value.EvaluateAsync(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback(() => {
                closed = closePopup;
                if (!closePopup) url = "https://example.test/destination";
            })
            .ReturnsAsync((JsonElement?)null);
        popupPage.Setup(value => value.EvaluateAsync<bool>(It.IsAny<string>(), It.IsAny<object?>()))
            .ThrowsAsync(new PlaywrightException("the blank realm is no longer available"));
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>())).Returns(Mock.Of<ICDPSessionEvent>());
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync((JsonElement?)null);
        session.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(popupPage.Object)).ReturnsAsync(session.Object);
        await using HtmlBrowserPopupHeaderCoordinator coordinator = new(
            context.Object, Mock.Of<IPage>(), new Uri("https://example.test"),
            new Dictionary<string, string>(), CancellationToken.None, () => { });

        context.Raise(value => value.Page += null, context.Object, popupPage.Object);
        await coordinator.WaitForPendingAsync(CancellationToken.None);

        popupPage.Verify(value => value.EvaluateAsync(It.IsAny<string>(), It.IsAny<object?>()), Times.Once);
        popupPage.Verify(value => value.EvaluateAsync<bool>(It.IsAny<string>(), It.IsAny<object?>()), Times.Never);
    }

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
