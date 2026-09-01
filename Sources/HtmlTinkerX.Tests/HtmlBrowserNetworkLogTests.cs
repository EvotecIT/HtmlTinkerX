using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests capturing network log entries with <see cref="HtmlBrowser"/>.
/// </summary>
public class HtmlBrowserNetworkLogTests {
    [Fact]
    public async Task ExportEvidenceAsync_NullOptionsCallRemainsSourceCompatible() {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HtmlBrowser.ExportEvidenceAsync(null!, "unused", null));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HtmlBrowser.ExportEvidenceAsync(null!, "unused", null, default));
    }

    [Fact]
    public async Task ExportEvidenceAsync_UsesExplicitNetworkScope() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://current.example.com/");
        page.Setup(p => p.TitleAsync()).ReturnsAsync("Current page");

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);
        Mock<IRequest> previousRequest = CreateRequest("https://previous.example.com/");
        Mock<IRequest> currentRequest = CreateRequest("https://current.example.com/");
        page.Raise(p => p.Request += null!, page.Object, previousRequest.Object);
        page.Raise(p => p.Request += null!, page.Object, currentRequest.Object);

        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlTinkerXTests", Guid.NewGuid().ToString("N"));
        try {
            HtmlNetworkEntry currentEntry = session.NetworkLog.Last();
            HtmlCrawlRenderedPageContext renderedPage = new(
                session,
                new HtmlCrawlPage { Url = page.Object.Url, Rendered = true },
                new[] { currentEntry });
            await HtmlBrowser.ExportRenderedPageEvidenceAsync(
                renderedPage,
                outputPath,
                new HtmlBrowserEvidenceOptions {
                    Screenshot = false,
                    FullPageScreenshot = false,
                    Pdf = false,
                    Html = false,
                    VisibleText = false,
                    Markdown = false,
                    NetworkSummary = true,
                    SsoHandoffSummary = false,
                    Manifest = false
                });

            string networkSummary = File.ReadAllText(Path.Combine(outputPath, "network-summary.json"));
            Assert.Contains("current.example.com", networkSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("previous.example.com", networkSummary, StringComparison.Ordinal);
        } finally {
            await session.DisposeAsync();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

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

    [Fact]
    public async Task CaptureResponseBodiesAsync_CanceledToken_Throws() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.com/api/data");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        request.SetupGet(r => r.ResourceType).Returns("fetch");

        var pendingBody = new TaskCompletionSource<string>();
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        response.Setup(r => r.TextAsync()).Returns(pendingBody.Task);

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, request.Object);
        page.Raise(p => p.Response += null!, page.Object, response.Object);

        using CancellationTokenSource cts = new();
        Task capture = session.CaptureResponseBodiesAsync(
            100,
            new HashSet<HtmlNetworkResourceType> { HtmlNetworkResourceType.Fetch },
            cts.Token);
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => capture);
        Assert.Null(HtmlBrowser.GetNetworkLog(session).Single().ResponseBodyError);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task CaptureResponseBodiesAsync_SkipsLargeDeclaredBodiesBeforeReading() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.com/api/large");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        request.SetupGet(r => r.ResourceType).Returns("fetch");

        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string> {
            ["content-length"] = "2097152"
        });
        response.Setup(r => r.TextAsync()).ThrowsAsync(new InvalidOperationException("Body should not be read."));

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, request.Object);
        page.Raise(p => p.Response += null!, page.Object, response.Object);

        await session.CaptureResponseBodiesAsync(100, new HashSet<HtmlNetworkResourceType> { HtmlNetworkResourceType.Fetch }, CancellationToken.None);

        HtmlNetworkEntry entry = HtmlBrowser.GetNetworkLog(session).Single();
        Assert.Null(entry.ResponseBody);
        Assert.True(entry.ResponseBodyTruncated);
        Assert.Contains("exceeds buffered capture limit", entry.ResponseBodyError);
        response.Verify(r => r.TextAsync(), Times.Never);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task CaptureResponseBodiesAsync_RedactsBeforeTruncatingBodies() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var request = new Mock<IRequest>();
        request.SetupGet(r => r.Url).Returns("https://example.com/api/secret");
        request.SetupGet(r => r.Method).Returns("GET");
        request.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        request.SetupGet(r => r.ResourceType).Returns("fetch");

        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Request).Returns(request.Object);
        response.SetupGet(r => r.Status).Returns(200);
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        response.Setup(r => r.TextAsync()).ReturnsAsync("{\"url\":\"https://example.com/callback?token=abc123456789&safe=1\",\"message\":\"padding\"}");

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Request += null!, page.Object, request.Object);
        page.Raise(p => p.Response += null!, page.Object, response.Object);

        await session.CaptureResponseBodiesAsync(55, new HashSet<HtmlNetworkResourceType> { HtmlNetworkResourceType.Fetch }, CancellationToken.None, redactSensitiveValues: true);

        HtmlNetworkEntry entry = HtmlBrowser.GetNetworkLog(session).Single();
        Assert.True(entry.ResponseBodyRedacted);
        Assert.True(entry.ResponseBodyTruncated);
        Assert.DoesNotContain("abc123", entry.ResponseBody);
        Assert.DoesNotContain("456789", entry.ResponseBody);
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
