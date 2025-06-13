namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public enum HtmlBrowserEngine {
    /// <summary>
    /// Chromium-based browser engine.
    /// </summary>
    Chromium,
    /// <summary>
    /// Firefox browser engine.
    /// </summary>
    Firefox,
    /// <summary>
    /// WebKit browser engine, used by Safari and others.
    /// </summary>
    Webkit,
}