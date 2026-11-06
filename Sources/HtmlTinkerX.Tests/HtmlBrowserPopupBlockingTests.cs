using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserPopupBlockingTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium, false)]
    [InlineData(HtmlBrowserEngine.Chromium, true)]
    [InlineData(HtmlBrowserEngine.Firefox, false)]
    [InlineData(HtmlBrowserEngine.Firefox, true)]
    public async Task BlockPopupsStopsAPageFromOpeningAWindowWithoutAUserGesture(HtmlBrowserEngine browser, bool blockPopups) {
        var options = new HtmlBrowserLaunchOptions { Browser = browser, BlockPopups = blockPopups };
        await using var session = await HtmlBrowser.OpenSessionForNavigationAsync(options, default);
        await session.Page.RouteAsync("https://popups.test/**", route => route.FulfillAsync(new RouteFulfillOptions {
            ContentType = "text/html",
            Body = "<script>window.popupOpened = window.open('about:blank', '_blank') !== null;</script>"
        }));

        await HtmlBrowser.NavigateAsync(session, "https://popups.test/", HtmlBrowserLoadState.Load);

        Assert.Equal(!blockPopups, await session.Page.EvaluateAsync<bool>("() => window.popupOpened"));
    }

    [Fact]
    public async Task BlockPopupsIsRejectedOnWebKit() {
        var options = new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.WebKit, BlockPopups = true };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => HtmlBrowser.OpenSessionForNavigationAsync(options, default));

        Assert.Contains("BlockPopups", error.Message);
    }
}
