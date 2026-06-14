using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserSnapshotTests {
    [Fact]
    public async Task CreateSnapshotAsync_WaitsForSelectorBeforeReadingDocumentSnapshot() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        var locator = new Mock<ILocator>();
        bool selectorReady = false;

        locator.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>()))
            .Callback(() => selectorReady = true)
            .Returns(Task.CompletedTask);
        locator.Setup(l => l.InnerTextAsync(It.IsAny<LocatorInnerTextOptions?>()))
            .ReturnsAsync("ready text");
        page.Setup(p => p.Locator("#ready", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);
        page.SetupGet(p => p.Url).Returns("https://example.test/page");
        page.Setup(p => p.TitleAsync()).ReturnsAsync("Snapshot");
        page.Setup(p => p.ContentAsync()).ReturnsAsync(() => selectorReady
            ? "<!doctype html><html><body><main id=\"ready\">ready text</main></body></html>"
            : "<!doctype html><html><body><main>loading</main></body></html>");
        page.Setup(p => p.InnerTextAsync("html", It.IsAny<PageInnerTextOptions?>()))
            .ReturnsAsync(() => selectorReady ? "ready text" : "loading");

        HtmlBrowserSession session = new(
            playwright.Object,
            browser.Object,
            context.Object,
            page.Object,
            network: new ConcurrentDictionary<IRequest, HtmlNetworkEntry>());

        HtmlRenderedPageSnapshot snapshot = await HtmlBrowser.CreateSnapshotAsync(
            session,
            "https://example.test/page",
            selector: "#ready",
            asText: true);

        Assert.Equal("ready text", snapshot.Content);
        Assert.Contains("ready text", snapshot.Html);
        Assert.DoesNotContain("loading", snapshot.Html);
        Assert.Contains("ready text", snapshot.Text);
    }
}
