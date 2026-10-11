using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Cancellation timing")]
public sealed class HtmlFormBrowserOwnershipTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledSubmissionDisposesHandlesReturnedLaterWithoutClosingCallerPage(bool readingProperties) {
        Mock<IPage> page = new();
        Mock<ILocator> form = new();
        Mock<IJSHandle> controls = new();
        Mock<IJSHandle> property = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IJSHandle> lateControls = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Dictionary<string, IJSHandle>> lateProperties = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> controlsDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> propertyDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Setup(value => value.Locator("form", It.IsAny<PageLocatorOptions?>())).Returns(form.Object);
        form.Setup(value => value.WaitForAsync(It.IsAny<LocatorWaitForOptions?>())).Returns(Task.CompletedTask);
        controls.Setup(value => value.DisposeAsync()).Callback(() => controlsDisposed.TrySetResult(true)).Returns(default(ValueTask));
        property.Setup(value => value.DisposeAsync()).Callback(() => propertyDisposed.TrySetResult(true)).Returns(default(ValueTask));
        if (readingProperties) {
            form.Setup(value => value.EvaluateHandleAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(controls.Object);
            controls.Setup(value => value.GetPropertiesAsync()).Callback(() => entered.TrySetResult(true)).Returns(lateProperties.Task);
        } else {
            form.Setup(value => value.EvaluateHandleAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Callback(() => entered.TrySetResult(true)).Returns(lateControls.Task);
        }

        using CancellationTokenSource stopping = new();
        Task submission = HtmlFormSubmitter.SubmitAsync(page.Object, "form", new Dictionary<string, string> { ["q"] = "updated" }, cancellationToken: stopping.Token);
        try {
            await AwaitBoundedAsync(entered.Task);
            stopping.Cancel();
            Task<OperationCanceledException> canceled = Assert.ThrowsAnyAsync<OperationCanceledException>(() => submission);
            await AwaitBoundedAsync(canceled);
            Assert.Equal(stopping.Token, (await canceled).CancellationToken);
            if (readingProperties) {
                Assert.False(lateProperties.Task.IsCompleted);
                lateProperties.SetResult(new Dictionary<string, IJSHandle> { ["0"] = property.Object });
                await AwaitBoundedAsync(propertyDisposed.Task);
            } else {
                Assert.False(lateControls.Task.IsCompleted);
                lateControls.SetResult(controls.Object);
            }
            await AwaitBoundedAsync(controlsDisposed.Task);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
        } finally {
            stopping.Cancel();
            lateControls.TrySetResult(controls.Object);
            lateProperties.TrySetResult(new Dictionary<string, IJSHandle> { ["0"] = property.Object });
        }
    }

    private static async Task AwaitBoundedAsync(Task pending) {
        Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5))));
        await pending;
    }
}
