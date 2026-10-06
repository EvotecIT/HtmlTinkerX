using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserInitialReadinessTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeferredInitialNavigationPreservesLoginAndNormalNavigationPolicy(bool deferInitialNavigation, bool login) {
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        var browser = new Mock<IBrowser>();
        var browserType = new Mock<IBrowserType>();
        var playwright = new Mock<IPlaywright>();
        var navigations = new List<(string Url, WaitUntilState? State, float? Timeout)>();
        page.Setup(value => value.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .Callback<string, PageGotoOptions>((url, options) => navigations.Add((url, options.WaitUntil, options.Timeout)))
            .ReturnsAsync((IResponse?)null);
        context.Setup(value => value.NewPageAsync()).ReturnsAsync(page.Object);
        browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        browserType.Setup(value => value.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        var originalFactory = HtmlBrowser.PlaywrightFactory;
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        try {
            var options = new HtmlBrowserLaunchOptions {
                BrowserChannel = "msedge",
                Timeout = 1379,
                FormLogin = login ? new HtmlFormLogin { LoginUrl = "https://example.test/login", SubmitSelector = "#submit" } : null
            };
            await using var session = deferInitialNavigation
                ? await HtmlBrowser.OpenSessionForNavigationAsync(options, default)
                : await HtmlBrowser.OpenSessionAsync("about:blank", options);
            Assert.Equal((login ? 1 : 0) + (deferInitialNavigation ? 0 : 1), navigations.Count);
            if (!deferInitialNavigation) {
                Assert.Equal(("about:blank", (WaitUntilState?)WaitUntilState.NetworkIdle, (float?)1379), navigations[navigations.Count - 1]);
            }
            if (login) {
                Assert.Equal(("https://example.test/login", (WaitUntilState?)WaitUntilState.NetworkIdle, (float?)1379), navigations[0]);
                page.Verify(value => value.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    It.Is<PageWaitForLoadStateOptions>(options => options.Timeout == 1379)), Times.Once);
            }
        } finally {
            HtmlBrowser.PlaywrightFactory = originalFactory;
        }
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task DeferredInitialNavigationRunsInitScriptsOnTheActualDestination(HtmlBrowserEngine browser) {
        var options = new HtmlBrowserLaunchOptions {
            Browser = browser,
            InitScripts = { "window.initializationCount = (window.initializationCount || 0) + 1" }
        };
        await using var session = await HtmlBrowser.OpenSessionForNavigationAsync(options, default);
        await session.Page.RouteAsync("https://initialization.test/**", route => route.FulfillAsync(new RouteFulfillOptions {
            ContentType = "text/html", Body = "<h1>Destination</h1>"
        }));

        await HtmlBrowser.NavigateAsync(session, "https://initialization.test/", HtmlBrowserLoadState.DomContentLoaded);

        Assert.Equal("https://initialization.test/", session.Page.Url);
        Assert.Equal(1, await session.Page.EvaluateAsync<int>("() => window.initializationCount"));
    }
}
