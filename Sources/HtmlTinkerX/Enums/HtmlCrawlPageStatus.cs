namespace HtmlTinkerX;

/// <summary>
/// Describes the outcome of a crawl candidate.
/// </summary>
public enum HtmlCrawlPageStatus {
    /// <summary>The page was fetched successfully.</summary>
    Success,

    /// <summary>The page fetch was attempted but failed.</summary>
    Failed,

    /// <summary>The page was not fetched because crawl rules skipped it.</summary>
    Skipped
}
