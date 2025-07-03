using System.Collections.Generic;
using System.Linq;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserConsoleLogTests
{
    [Fact]
    public async Task GetConsoleLog_ReturnsCapturedEntries()
    {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var msg1 = new Mock<IConsoleMessage>();
        msg1.SetupGet(m => m.Text).Returns("first");
        msg1.SetupGet(m => m.Type).Returns("log");
        msg1.SetupGet(m => m.Location).Returns("loc1");

        var msg2 = new Mock<IConsoleMessage>();
        msg2.SetupGet(m => m.Text).Returns("second");
        msg2.SetupGet(m => m.Type).Returns("error");
        msg2.SetupGet(m => m.Location).Returns("loc2");

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Console += null!, page.Object, msg1.Object);
        page.Raise(p => p.Console += null!, page.Object, msg2.Object);

        List<HtmlConsoleEntry> log = HtmlBrowser.GetConsoleLog(session).ToList();

        Assert.Equal(2, log.Count);
        Assert.Equal("first", log[0].Text);
        Assert.Equal("log", log[0].Type);
        Assert.Equal("loc1", log[0].Location);
        Assert.Equal("second", log[1].Text);
        Assert.Equal("error", log[1].Type);
        Assert.Equal("loc2", log[1].Location);
        await session.DisposeAsync();
    }
}
