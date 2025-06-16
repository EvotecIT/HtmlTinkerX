using System.Collections.Generic;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Returns network log captured in the specified session.
    /// </summary>
    public static IEnumerable<HtmlNetworkEntry> GetNetworkLog(HtmlBrowserSession session)
        => session.NetworkLog;
}
