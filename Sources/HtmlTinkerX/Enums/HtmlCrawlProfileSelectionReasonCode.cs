namespace HtmlTinkerX;

/// <summary>
/// Describes why a crawl profile was selected for a crawl or page.
/// </summary>
public enum HtmlCrawlProfileSelectionReasonCode {
    /// <summary>No profile selection reason was recorded.</summary>
    None = 0,

    /// <summary>The caller explicitly selected a profile by name.</summary>
    ExplicitProfileName = 1,

    /// <summary>Auto-profile matched a profile by the starting host name.</summary>
    AutoProfileHostMatch = 2,

    /// <summary>Auto-profile inferred a WordPress-oriented profile from WordPress markers in the fetched page.</summary>
    AutoProfileWordPressMarkers = 3,

    /// <summary>Auto-profile inferred a documentation-oriented profile from documentation markers in the fetched page.</summary>
    AutoProfileDocumentationMarkers = 4,

    /// <summary>Auto-profile inferred an API-documentation profile from API-doc markers in the fetched page.</summary>
    AutoProfileApiDocumentationMarkers = 5
}
