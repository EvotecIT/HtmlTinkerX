using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserPopupBlockingTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium, false, false)]
    [InlineData(HtmlBrowserEngine.Chromium, true, false)]
    [InlineData(HtmlBrowserEngine.Chromium, true, true)]
    [InlineData(HtmlBrowserEngine.Firefox, false, false)]
    [InlineData(HtmlBrowserEngine.Firefox, true, false)]
    [InlineData(HtmlBrowserEngine.Firefox, true, true)]
    public async Task BlockPopupsStopsAPageFromOpeningAWindowWithoutAUserGesture(HtmlBrowserEngine browser, bool blockPopups, bool persistent) {
        string? profile = persistent ? Path.Combine(Path.GetTempPath(), "htmltinkerx-popups-" + Guid.NewGuid().ToString("N")) : null;
        var options = new HtmlBrowserLaunchOptions { Browser = browser, BlockPopups = blockPopups, UserDataDirectory = profile };
        try {
            await AssertPopupOpenedAsync(options, !blockPopups);
        } finally {
            if (profile != null && Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
        }
    }

    private static async Task AssertPopupOpenedAsync(HtmlBrowserLaunchOptions options, bool expected) {
        await using var session = await HtmlBrowser.OpenSessionForNavigationAsync(options, default);
        await session.Page.RouteAsync("https://popups.test/**", route => route.FulfillAsync(new RouteFulfillOptions {
            ContentType = "text/html",
            Body = "<script>window.popupOpened = window.open('about:blank', '_blank') !== null;</script>"
        }));

        await HtmlBrowser.NavigateAsync(session, "https://popups.test/", HtmlBrowserLoadState.Load);

        Assert.Equal(expected, await session.Page.EvaluateAsync<bool>("() => window.popupOpened"));
    }

    [Fact]
    public async Task BlockPopupsIsRejectedOnWebKit() {
        var options = new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.WebKit, BlockPopups = true };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => HtmlBrowser.OpenSessionForNavigationAsync(options, default));

        Assert.Contains("BlockPopups", error.Message);
    }
}
