using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests capturing network log entries with <see cref="HtmlBrowser"/>.
/// </summary>
public class HtmlBrowserNetworkLogTests {
    [Fact]
    public async Task GetNetworkLog_ReturnsCapturedEntries() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.com/");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string> { { "h1", "v1" } });

        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string> { { "h2", "v2" } });

        var session = new HtmlBrowserSession(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, request.Object);
        page.Raise(p => p.Response += null!, page.Object, response.Object);
        page.Raise(p => p.RequestFinished += null!, page.Object, request.Object);

        HtmlNetworkEntry entry = Assert.Single(HtmlBrowser.GetNetworkLog(session));
        Assert.Equal("https://example.com/", entry.Url);
        Assert.Equal(HtmlHttpMethod.Get, entry.Method);
        Assert.Equal(System.Net.HttpStatusCode.OK, entry.Status);
        Assert.Equal("v1", entry.RequestHeaders["h1"]);
        Assert.Equal("v2", entry.ResponseHeaders!["h2"]);
        Assert.NotNull(entry.Duration);
        await session.DisposeAsync();
    }

#if NETFRAMEWORK
    [Fact]
    public Task NetworkLog_PreservesRequestOrder_NetFramework() => VerifyNetworkLogOrderAsync();
#else
    [Fact]
    public Task NetworkLog_PreservesRequestOrder() => VerifyNetworkLogOrderAsync();
#endif

    private static async Task VerifyNetworkLogOrderAsync() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        List<Mock<IRequest>> requests = new List<Mock<IRequest>> {
            CreateRequest("https://1.example.com"),
            CreateRequest("https://2.example.com"),
            CreateRequest("https://3.example.com")
        };

        foreach (Mock<IRequest> request in requests) {
            page.Raise(p => p.Request += null!, page.Object, request.Object);
        }

        List<HtmlNetworkEntry> sessionLog = session.NetworkLog.ToList();
        Assert.Equal(requests.Select(r => r.Object.Url), sessionLog.Select(e => e.Url));

        List<HtmlNetworkEntry> browserLog = HtmlBrowser.GetNetworkLog(session).ToList();
        Assert.Equal(requests.Select(r => r.Object.Url), browserLog.Select(e => e.Url));

        await session.DisposeAsync();
    }

    private static Mock<IRequest> CreateRequest(string url) {
        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns(url);
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        return request;
    }
}
