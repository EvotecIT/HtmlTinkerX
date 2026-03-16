namespace HtmlTinkerX;

/// <summary>
/// Applies a built-in structured JSON extraction preset on top of the base crawl document model.
/// </summary>
public enum HtmlCrawlStructuredJsonPreset {
    /// <summary>Do not add any preset extracted fields.</summary>
    None = 0,

    /// <summary>Choose a preset automatically per page using lightweight content heuristics.</summary>
    Auto = 1,

    /// <summary>Flatten common documentation-page fields such as headings, navigation, and code blocks.</summary>
    Docs = 2,

    /// <summary>Flatten common article-style fields such as author, dates, lead text, and section headings.</summary>
    Article = 3,

    /// <summary>Flatten common product-page fields such as price, SKU, availability, and breadcrumbs.</summary>
    Product = 4
}
