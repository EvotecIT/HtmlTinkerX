using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserNetworkLogLimitTests {
    [Fact]
    public async Task NetworkLogLimit_TrimsOldEntries() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var req1 = new Mock<IRequest>();
        req1.SetupGet(r => r.Url).Returns("https://1.com");
        req1.SetupGet(r => r.Method).Returns("GET");
        req1.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());

        var req2 = new Mock<IRequest>();
        req2.SetupGet(r => r.Url).Returns("https://2.com");
        req2.SetupGet(r => r.Method).Returns("GET");
        req2.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());

        var req3 = new Mock<IRequest>();
        req3.SetupGet(r => r.Url).Returns("https://3.com");
        req3.SetupGet(r => r.Method).Returns("GET");
        req3.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);
        session.NetworkLogLimit = 2;

        page.Raise(p => p.Request += null!, page.Object, req1.Object);
        page.Raise(p => p.Request += null!, page.Object, req2.Object);
        page.Raise(p => p.Request += null!, page.Object, req3.Object);

        Assert.Equal(2, session.NetworkLog.Count());
        Assert.DoesNotContain(session.NetworkLog, e => e.Url == "https://1.com");
        await session.DisposeAsync();
    }

    [Fact]
    public async Task NetworkLogLimit_Null_KeepsAllEntries() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var req1 = new Mock<IRequest>();
        req1.SetupGet(r => r.Url).Returns("https://1.com");
        req1.SetupGet(r => r.Method).Returns("GET");
        req1.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());

        var req2 = new Mock<IRequest>();
        req2.SetupGet(r => r.Url).Returns("https://2.com");
        req2.SetupGet(r => r.Method).Returns("GET");
        req2.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, req1.Object);
        page.Raise(p => p.Request += null!, page.Object, req2.Object);

        Assert.Equal(2, session.NetworkLog.Count());
        await session.DisposeAsync();
    }
}
