namespace HtmlTinkerX;

/// <summary>
/// Describes how page content was fetched for a crawl entry.
/// </summary>
public enum HtmlCrawlRenderMode {
    /// <summary>Fetched through normal HTTP without browser rendering.</summary>
    Static,

    /// <summary>Fetched through Playwright because rendering was explicitly requested.</summary>
    Rendered,

    /// <summary>Fetched statically first, then retried through Playwright after auto-render heuristics triggered.</summary>
    AutoRendered
}
