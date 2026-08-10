using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests video recording capabilities of <see cref="HtmlBrowser"/>.
/// </summary>
[Collection("Playwright collection")]
public class HtmlBrowserVideoRecordingTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartStopVideoRecordingAsync_SavesAndCleans(bool useSession) {
        string outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string outFile = Path.Combine(outDir, "video.webm");
        string tempVideo = Path.GetTempFileName();
        File.WriteAllText(tempVideo, "tmp");

        var video = new Mock<IVideo>();
        video.Setup(v => v.SaveAsAsync(It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();
        video.Setup(v => v.PathAsync()).ReturnsAsync(tempVideo);

        var page = new Mock<IPage>();
        page.SetupGet(p => p.Video).Returns(video.Object);
        page.SetupGet(p => p.Url).Returns("https://example.com");
        page.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>())).ReturnsAsync((IResponse?)null);
        page.Setup(p => p.WaitForLoadStateAsync(It.IsAny<LoadState?>(), It.IsAny<PageWaitForLoadStateOptions>())).Returns(Task.CompletedTask);

        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.NewPageAsync()).ReturnsAsync(page.Object);
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
        string? statePath = null;
        context.Setup(c => c.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions>()))
            .Callback<BrowserContextStorageStateOptions>(o => { statePath = o.Path; File.WriteAllText(statePath!, "{}"); })
            .ReturnsAsync("{}");

        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);
        browser.SetupGet(b => b.BrowserType).Returns(new Mock<IBrowserType>().Object);

        var browserType = new Mock<IBrowserType>();
        browserType.Setup(bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);

        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(p => p.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);

        HtmlBrowserSession session;
        if (useSession) {
            var initContext = new Mock<IBrowserContext>();
            initContext.Setup(c => c.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions>())).ReturnsAsync("{}");
            var initBrowser = new Mock<IBrowser>();
            initBrowser.SetupGet(b => b.BrowserType).Returns(browserType.Object);
            var initPage = new Mock<IPage>();
            initPage.SetupGet(p => p.Url).Returns("https://example.com");
            session = await HtmlBrowser.StartVideoRecordingAsync(new HtmlBrowserSession(playwright.Object, initBrowser.Object, initContext.Object, initPage.Object), outFile);
        } else {
            session = await HtmlBrowser.StartVideoRecordingAsync("https://example.com", outFile);
        }

        await HtmlBrowser.StopVideoRecordingAsync(session);

        video.Verify(v => v.SaveAsAsync(outFile.ToFullPath()), Times.Once);
        Assert.False(File.Exists(tempVideo));
        if (useSession) {
            Assert.False(File.Exists(statePath!));
        }

        HtmlBrowser.PlaywrightFactory = null;
    }

    [Fact]
    public async Task StartVideoRecordingAsync_RemovesTempStorageFileOnFailure() {
        string? statePath = null;

        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://example.com");

        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions>()))
            .Callback<BrowserContextStorageStateOptions>(o => {
                statePath = o.Path;
                if (statePath != null) {
                    File.WriteAllText(statePath, "{}");
                }
            })
            .ReturnsAsync("{}");
        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);

        var browserType = new Mock<IBrowserType>();
        browserType.SetupGet(bt => bt.Name).Returns("chromium");

        var browser = new Mock<IBrowser>();
        browser.SetupGet(b => b.BrowserType).Returns(browserType.Object);
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);

        var existingPlaywright = new Mock<IPlaywright>();

        HtmlBrowserSession session = new(existingPlaywright.Object, browser.Object, context.Object, page.Object);

        var failingBrowserType = new Mock<IBrowserType>();
        failingBrowserType.Setup(bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ThrowsAsync(new SerializationException("Launch failed"));

        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(p => p.Chromium).Returns(failingBrowserType.Object);

        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);

        try {
            await Assert.ThrowsAsync<SerializationException>(() => HtmlBrowser.StartVideoRecordingAsync(session, "video.webm"));
            Assert.NotNull(statePath);
            Assert.False(File.Exists(statePath!));
        } finally {
            HtmlBrowser.PlaywrightFactory = null;
            await session.DisposeAsync();
            if (statePath != null && File.Exists(statePath)) {
                File.Delete(statePath);
            }
        }
    }
}
