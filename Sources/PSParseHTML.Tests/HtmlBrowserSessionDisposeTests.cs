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

    [Fact]
    public async Task DisposeAsync_SavesAndCleansVideo() {
        string outFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString(), "video.webm");
        string tempVideo = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempVideo, "tmp");

        var video = new Moq.Mock<IVideo>();
        video.Setup(v => v.SaveAsAsync(It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();
        video.Setup(v => v.PathAsync()).ReturnsAsync(tempVideo);

        var playwright = new Moq.Mock<IPlaywright>();
        var browser = new Moq.Mock<IBrowser>();
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var context = new Moq.Mock<IBrowserContext>();
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var page = new Moq.Mock<IPage>();

        HtmlBrowserSession session = new(playwright.Object, browser.Object, context.Object, page.Object, video.Object, outFile);

        await session.DisposeAsync();

        video.Verify(v => v.SaveAsAsync(HtmlUtilities.ResolvePath(outFile)), Moq.Times.Once);
        Assert.False(System.IO.File.Exists(tempVideo));
        browser.Verify();
        context.Verify();
    }
}
