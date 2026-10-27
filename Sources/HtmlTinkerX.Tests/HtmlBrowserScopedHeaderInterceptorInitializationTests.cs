using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserScopedHeaderInterceptorInitializationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrameTreeFailureDetachesSessionAndPreservesOriginalError(bool detachFails) {
        var failure = new PlaywrightException("Page closed while reading the frame tree.");
        var session = new Mock<ICDPSession>(MockBehavior.Strict);
        session.Setup(value => value.SendAsync("Page.getFrameTree", It.IsAny<Dictionary<string, object>?>()))
            .ThrowsAsync(failure);
        session.Setup(value => value.DetachAsync()).Returns(detachFails
            ? Task.FromException(new PlaywrightException("Session already detached."))
            : Task.CompletedTask);
        var page = new Mock<IPage>(MockBehavior.Strict);
        var context = new Mock<IBrowserContext>(MockBehavior.Strict);
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(session.Object);

        var actual = await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            context.Object, page.Object, null, new Dictionary<string, string>(), CancellationToken.None));

        Assert.Same(failure, actual);
        session.Verify(value => value.DetachAsync(), Times.Once);
        session.Verify(value => value.SendAsync("Network.enable", It.IsAny<Dictionary<string, object>?>()), Times.Never);
    }
}
