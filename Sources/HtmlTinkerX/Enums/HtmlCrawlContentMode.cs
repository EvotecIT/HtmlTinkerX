namespace HtmlTinkerX;

/// <summary>
/// Controls how crawl extraction chooses the HTML region used for stored content and text conversion.
/// </summary>
public enum HtmlCrawlContentMode {
    /// <summary>
    /// Uses only the exact selector when one is provided and otherwise keeps the full page.
    /// No semantic fallback is attempted for missing selectors.
    /// </summary>
    Raw,

    /// <summary>
    /// Uses the configured selector when present and otherwise falls back to common semantic content containers.
    /// This is the default balanced mode.
    /// </summary>
    Focused,

    /// <summary>
    /// Chooses the most article-like content block within the selected area or page by scoring likely reader content.
    /// </summary>
    Reader
}
