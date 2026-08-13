using Microsoft.Playwright;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserCdpOwnershipTests {
    [Fact]
    public async Task StalledPageCreationReleasesTheLocalCdpDriverWithinTheCleanupBound() {
        TaskCompletionSource<IPage> pageCreation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IPlaywright> owner = new();
        owner.Setup(instance => instance.Dispose()).Callback(() => disposed.TrySetResult(true));

        HtmlBrowser.ClosePageWhenCreated(pageCreation.Task, owner.Object, TimeSpan.FromMilliseconds(50));

        Task deadline = Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Same(disposed.Task, await Task.WhenAny(disposed.Task, deadline));
        owner.Verify(instance => instance.Dispose(), Times.Once);
    }
}
