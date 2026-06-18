using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Mobile device emulation helpers for browser sessions.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Provides viewport and user agent settings for well known mobile devices.
    /// </summary>
    public static HtmlMobileDeviceInfo GetMobileDeviceInfo(HtmlMobileDevice device) => device switch {
        HtmlMobileDevice.IPhone12 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1",
            ViewportWidth = 390,
            ViewportHeight = 844
        },
        HtmlMobileDevice.Pixel5 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0 Mobile Safari/537.36",
            ViewportWidth = 393,
            ViewportHeight = 851
        },
        HtmlMobileDevice.GalaxyS8 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (Linux; Android 7.0; SM-G950U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0 Mobile Safari/537.36",
            ViewportWidth = 360,
            ViewportHeight = 740
        },
        _ => throw new ArgumentOutOfRangeException(nameof(device))
    };

    /// <summary>
    /// Applies mobile device emulation settings to an existing session.
    /// </summary>
    public static async Task SetMobileDeviceAsync(HtmlBrowserSession session, HtmlMobileDevice device, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        HtmlMobileDeviceInfo info = GetMobileDeviceInfo(device);
        cancellationToken.ThrowIfCancellationRequested();
        await session.Context.AddInitScriptAsync($"Object.defineProperty(navigator, 'userAgent', {{ get: () => '{info.UserAgent}' }});").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await session.Page.SetViewportSizeAsync(info.ViewportWidth, info.ViewportHeight).ConfigureAwait(false);
    }
}
