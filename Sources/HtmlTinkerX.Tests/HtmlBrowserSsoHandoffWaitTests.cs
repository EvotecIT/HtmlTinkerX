using Microsoft.Playwright;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Cancellation timing")]
public class HtmlBrowserSsoHandoffWaitTests {
    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public async Task WaitReturnsAHandoffThatAppearsAfterAnEmptyRead(int timeout) {
        Mock<IPage> page = new();
        page.SetupGet(value => value.Url).Returns("https://login.test/wait");
        page.SetupSequence(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
            .ReturnsAsync("{\"forms\":[]}")
            .ReturnsAsync("{\"forms\":[{\"fields\":[{\"name\":\"SAMLResponse\",\"type\":\"hidden\",\"value\":\"private-assertion\"}]}]}");
        page.Setup(value => value.WaitForTimeoutAsync(It.IsAny<float>())).Returns(Task.CompletedTask);
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object,
            new Mock<IBrowserContext>().Object, page.Object);

        HtmlBrowserSsoHandoff handoff = Assert.Single(await HtmlBrowser.GetSsoHandoffsAsync(session,
            new HtmlBrowserSsoHandoffOptions { Wait = true, Timeout = timeout }));

        Assert.Equal(HtmlBrowserSsoHandoffKind.Saml, handoff.Kind);
        Assert.Equal("<redacted>", handoff.FormData["SAMLResponse"]);
        page.Verify(value => value.WaitForTimeoutAsync(It.IsAny<float>()), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutReturnsWhileTheBrowserReadOrPollIsPending(bool polling) {
        Mock<IPage> page = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        page.SetupGet(value => value.Url).Returns("https://login.test/wait?password=private-value&tenant=public");
        if (polling) {
            page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
                .ReturnsAsync("{\"title\":\"Sign in\",\"forms\":[]}");
            page.Setup(value => value.WaitForTimeoutAsync(It.IsAny<float>()))
                .Callback(() => entered.TrySetResult(true)).Returns(pending.Task);
        } else {
            page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
                .Callback(() => entered.TrySetResult(true)).Returns(pending.Task);
        }
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object,
            new Mock<IBrowserContext>().Object, page.Object);
        using CancellationTokenSource cancellation = new();
        Task operation = HtmlBrowser.GetSsoHandoffsAsync(session,
            new HtmlBrowserSsoHandoffOptions { Wait = true, Timeout = 100 }, cancellation.Token);
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(2000)));
            Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(2000)));
            TimeoutException error = await Assert.ThrowsAsync<TimeoutException>(() => operation);
            Assert.Contains("100 ms", error.Message);
            Assert.Contains("tenant=public", error.Message);
            Assert.DoesNotContain("private-value", error.Message);
            if (polling) Assert.Contains("Sign in", error.Message);
            Assert.False(pending.Task.IsCompleted);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
        } finally {
            cancellation.Cancel();
            pending.TrySetResult("{}");
            try { await operation; } catch (Exception) { }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public async Task CallerCancellationRetainsItsTokenDuringTheWait(int timeout) {
        Mock<IPage> page = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback(() => entered.TrySetResult(true)).Returns(pending.Task);
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object,
            new Mock<IBrowserContext>().Object, page.Object);
        using CancellationTokenSource cancellation = new();
        Task operation = HtmlBrowser.GetSsoHandoffsAsync(session,
            new HtmlBrowserSsoHandoffOptions { Wait = true, Timeout = timeout }, cancellation.Token);
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(2000)));
            if (timeout == 0) {
                await Task.Delay(250);
                Assert.False(operation.IsCompleted);
            }
            cancellation.Cancel();
            Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(2000)));
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.False(pending.Task.IsCompleted);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
        } finally {
            cancellation.Cancel();
            pending.TrySetResult("{}");
            try { await operation; } catch (Exception) { }
        }
    }
}
