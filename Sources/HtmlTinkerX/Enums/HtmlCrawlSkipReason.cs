namespace HtmlTinkerX;

/// <summary>
/// Explains why a crawl candidate was skipped.
/// </summary>
public enum HtmlCrawlSkipReason {
    /// <summary>No skip reason applies.</summary>
    None,

    /// <summary>The URL was outside the allowed host scope.</summary>
    OutsideHost,

    /// <summary>The URL was outside the allowed path scope.</summary>
    OutsidePathScope,

    /// <summary>The URL matched an exclude filter.</summary>
    ExcludedByPattern,

    /// <summary>The URL did not match an include filter.</summary>
    NotIncludedByPattern,

    /// <summary>The URL was disallowed by robots.txt.</summary>
    DisallowedByRobots,

    /// <summary>The URL was already queued or visited.</summary>
    Duplicate,

    /// <summary>The fetched page duplicated earlier content.</summary>
    DuplicateContent,

    /// <summary>The URL matched a known asset/document path pattern.</summary>
    AssetPath,

    /// <summary>The fetched resource was not an allowed page content type.</summary>
    UnsupportedContentType,

    /// <summary>The URL was invalid or unsupported.</summary>
    InvalidUrl
}
