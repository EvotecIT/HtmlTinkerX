namespace HtmlTinkerX;

/// <summary>
/// Describes an intent-focused crawl configuration that applies product-style defaults before profiles and explicit options refine them.
/// </summary>
public enum HtmlCrawlScenario {
    /// <summary>Do not apply any scenario defaults beyond the base crawl options.</summary>
    Custom = 0,

    /// <summary>Optimize for clean readable content extraction.</summary>
    Content = 1,

    /// <summary>Optimize for a browsable offline snapshot with local pages and assets.</summary>
    Archive = 2,

    /// <summary>Optimize for documentation-style pages with article-first extraction and docs-chrome cleanup.</summary>
    Docs = 3,

    /// <summary>Optimize for downstream dataset generation, diagnostics, and deduplicated content capture.</summary>
    Dataset = 4
}
