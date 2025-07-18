using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserConsoleLogTests {
    [Fact]
    public async Task GetConsoleLog_ReturnsCapturedEntries() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var message = new Mock<IConsoleMessage>();
        message.SetupGet(m => m.Text).Returns("hello");
        message.SetupGet(m => m.Type).Returns("log");
        message.SetupGet(m => m.Location).Returns("file.js:1:2");

        var session = new HtmlBrowserSession(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Console += null!, page.Object, message.Object);

        HtmlConsoleEntry entry = Assert.Single(HtmlBrowser.GetConsoleLog(session));
        Assert.Equal("hello", entry.Text);
        Assert.Equal("log", entry.Type);
        Assert.Equal("file.js:1:2", entry.Location);
        await session.DisposeAsync();
    }
}