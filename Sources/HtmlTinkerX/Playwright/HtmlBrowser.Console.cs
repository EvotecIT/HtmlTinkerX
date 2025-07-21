using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Returns console log captured in the specified session.
    /// </summary>
    /// <param name="session">Browser session containing console log entries.</param>
    public static IEnumerable<HtmlConsoleEntry> GetConsoleLog(HtmlBrowserSession session) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        return session.ConsoleLog;
    }
}