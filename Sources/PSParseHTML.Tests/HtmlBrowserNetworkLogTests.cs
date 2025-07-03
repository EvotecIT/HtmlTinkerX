using System.Collections.Generic;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserNetworkLogTests
{
    [Fact]
    public async Task GetNetworkLog_ReturnsCapturedEntries()
    {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.com/");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>{{"h1","v1"}});

        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>{{"h2","v2"}});

        var session = new HtmlBrowserSession(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, request.Object);
        page.Raise(p => p.Response += null!, page.Object, response.Object);

        HtmlNetworkEntry entry = Assert.Single(HtmlBrowser.GetNetworkLog(session));
        Assert.Equal("https://example.com/", entry.Url);
        Assert.Equal("GET", entry.Method);
        Assert.Equal(200, entry.Status);
        Assert.Equal("v1", entry.RequestHeaders["h1"]);
        Assert.Equal("v2", entry.ResponseHeaders!["h2"]);
        await session.DisposeAsync();
    }
}
