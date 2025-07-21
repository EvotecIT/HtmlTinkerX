using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Returns console log captured in the specified session.
    /// </summary>
    /// <param name="session">Browser session containing console log entries.</param>
    /// <param name="severity">Optional severity filter.</param>
    public static IEnumerable<HtmlConsoleEntry> GetConsoleLog(HtmlBrowserSession session, HtmlConsoleSeverity? severity = null) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (severity == null) {
            return session.ConsoleLog;
        }

        HtmlConsoleSeverity sev = severity.Value;
        return session.ConsoleLog.Where(e => sev switch {
            HtmlConsoleSeverity.Error => e.Type == HtmlConsoleMessageType.Error || e.Type == HtmlConsoleMessageType.Assert,
            HtmlConsoleSeverity.Warning => e.Type == HtmlConsoleMessageType.Warning,
            HtmlConsoleSeverity.Info => e.Type != HtmlConsoleMessageType.Error && e.Type != HtmlConsoleMessageType.Assert && e.Type != HtmlConsoleMessageType.Warning,
            _ => true
        });
    }
}