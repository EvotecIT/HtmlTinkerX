using System.Collections.Generic;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Returns console log captured in the specified session.
    /// </summary>
    public static IEnumerable<HtmlConsoleEntry> GetConsoleLog(HtmlBrowserSession session)
        => session.ConsoleLog;
}

