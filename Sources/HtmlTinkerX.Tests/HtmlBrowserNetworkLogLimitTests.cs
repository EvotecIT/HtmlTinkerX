using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserNetworkLogLimitTests {
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(0)]
    public async Task NetworkLog_CapturesResponsesForRequestsStartedBeforeSubscription(int? limit) {
        var page = new Mock<IPage>();
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, null,
            new Mock<IBrowserContext>().Object, page.Object);
        session.NetworkLogLimit = limit;
        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.org/in-flight");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        request.SetupGet(r => r.ResourceType).Returns("fetch");
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        response.Setup(r => r.TextAsync()).ReturnsAsync("body");
        long cursor = session.NetworkLogPosition;
        page.Raise(p => p.Response += null!, page.Object, response.Object);
        if (limit == 0) { Assert.Empty(session.NetworkLog); return; }
        HtmlNetworkEntry entry = Assert.Single(session.GetNetworkLogSince(cursor));
        Assert.Equal(System.Net.HttpStatusCode.OK, entry.Status);
        await session.CaptureResponseBodiesAsync(128, new HashSet<HtmlNetworkResourceType> { entry.ResourceType }, default);
        Assert.Equal("body", entry.ResponseBody);
    }

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
    public async Task NetworkLogLimit_LateResponseCannotResurrectEvictedRequests() {
        var page = new Mock<IPage>();
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, null,
            new Mock<IBrowserContext>().Object, page.Object);
        session.NetworkLogLimit = 1;
        var requests = Enumerable.Range(0, 2).Select(index => {
            var request = new Mock<IRequest>();
            request.SetupGet(r => r.Url).Returns($"https://example.org/{index}");
            request.SetupGet(r => r.Method).Returns("GET");
            request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
            return request;
        }).ToArray();
        foreach (var request in requests) page.Raise(p => p.Request += null!, page.Object, request.Object);
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(requests[0].Object);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        page.Raise(p => p.Response += null!, page.Object, response.Object);
        Assert.Equal("https://example.org/1", Assert.Single(session.NetworkLog).Url);
        session.NetworkLogLimit = 0;
        Assert.Empty(session.NetworkLog);
    }

    [Fact]
    public async Task NetworkLogCursor_ReturnsNewRequestsWhenTheBoundedLogIsFull() {
        var page = new Mock<IPage>();
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, null,
            new Mock<IBrowserContext>().Object, page.Object);
        session.NetworkLogLimit = 1;
        var requests = Enumerable.Range(0, 2).Select(index => {
            var request = new Mock<IRequest>();
            request.SetupGet(r => r.Url).Returns($"https://example.org/{index}");
            request.SetupGet(r => r.Method).Returns("GET");
            request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
            return request;
        }).ToArray();
        page.Raise(p => p.Request += null!, page.Object, requests[0].Object);
        long cursor = session.NetworkLogPosition;
        page.Raise(p => p.Request += null!, page.Object, requests[1].Object);
        Assert.Equal("https://example.org/1", Assert.Single(session.GetNetworkLogSince(cursor)).Url);
        Assert.Empty(session.GetNetworkLogSince(session.NetworkLogPosition));
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
