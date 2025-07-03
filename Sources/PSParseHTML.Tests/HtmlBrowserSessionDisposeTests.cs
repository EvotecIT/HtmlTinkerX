using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using Xunit;
using PSParseHTML;

namespace PSParseHTML.Tests;

public class HtmlBrowserSessionDisposeTests {
    [Fact]
    public async Task DisposeAsync_AllowsNullProperties() {
        var session = (HtmlBrowserSession)FormatterServices.GetUninitializedObject(typeof(HtmlBrowserSession));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllPlaywrightObjects() {
        var playwright = new Mock<IPlaywright>();
        playwright.Setup(p => p.Dispose()).Verifiable();
        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        await session.DisposeAsync();

        playwright.Verify();
        browser.Verify();
        context.Verify();
    }
}
