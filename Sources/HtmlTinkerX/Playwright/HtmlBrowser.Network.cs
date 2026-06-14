using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Returns network log captured in the specified session.
    /// </summary>
    /// <param name="session">Browser session containing network data.</param>
    public static IEnumerable<HtmlNetworkEntry> GetNetworkLog(HtmlBrowserSession session)
        => session.NetworkLog;
}