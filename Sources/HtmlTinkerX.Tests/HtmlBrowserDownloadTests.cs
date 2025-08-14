using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserDownloadTests {
    [Fact]
    public async Task OpenSessionAsync_InvalidUrl_LogsError() {
        var page = new Mock<IPage>();
        page.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .ThrowsAsync(new PlaywrightException("boom"));

        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.NewPageAsync()).ReturnsAsync(page.Object);

        var browser = new Mock<IBrowser>();
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(context.Object);
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions>()))
            .Returns(Task.CompletedTask);
        browser.SetupGet(b => b.BrowserType).Returns(new Mock<IBrowserType>().Object);

        var browserType = new Mock<IBrowserType>();
        browserType.Setup(bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(browser.Object);

        var playwright = new Mock<IPlaywright>();
        playwright.SetupGet(p => p.Chromium).Returns(browserType.Object);
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);

        InternalLogger originalLogger = LoggingMessages.Logger;
        InternalLogger logger = new InternalLogger();
        List<LogEventArgs> errors = new();
        logger.OnErrorMessage += (_, e) => errors.Add(e);
        LoggingMessages.Logger = logger;

        var ex = await Record.ExceptionAsync(() => HtmlBrowser.OpenSessionAsync("https://invalid"));
        Assert.IsType<PlaywrightException>(ex);

        Assert.Single(errors);
        Assert.Contains("https://invalid", errors[0].Message);

        LoggingMessages.Logger = originalLogger;
        HtmlBrowser.PlaywrightFactory = null;
    }
}

