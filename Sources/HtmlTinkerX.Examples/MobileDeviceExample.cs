using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates how to open a page emulating a mobile device.
/// </summary>
public static class MobileDeviceExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        HtmlMobileDeviceInfo info = HtmlBrowser.GetMobileDeviceInfo(HtmlMobileDevice.IPhone12);
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(
            "https://example.com",
            userAgent: info.UserAgent,
            viewportWidth: info.ViewportWidth,
            viewportHeight: info.ViewportHeight).ConfigureAwait(false);
        Console.WriteLine(await session.Page.TitleAsync().ConfigureAwait(false));
        await HtmlBrowser.CloseSessionAsync(session).ConfigureAwait(false);
    }
}
