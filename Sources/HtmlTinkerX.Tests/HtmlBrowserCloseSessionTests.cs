using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserCloseSessionTests {
    [Fact]
    public async Task CloseSessionAsync_DisposesBrowserObjects() {
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(p => p.Dispose()).Verifiable();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()))
            .Returns(Task.CompletedTask).Verifiable();
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()))
            .Returns(Task.CompletedTask).Verifiable();
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        await HtmlBrowser.CloseSessionAsync(session, CancellationToken.None);

        playwright.Verify();
        browser.Verify();
        context.Verify();
    }
}