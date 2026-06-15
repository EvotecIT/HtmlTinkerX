namespace HtmlTinkerX;

/// <summary>
/// Recommended workflow for extracting useful information from an HTML page.
/// </summary>
public enum HtmlExtractionPlanMode {
    /// <summary>Static parsing is likely enough.</summary>
    Static,
    /// <summary>Browser rendering or a rendered snapshot is likely needed.</summary>
    RenderedSnapshot,
    /// <summary>The page appears to be part of a larger crawlable dataset.</summary>
    Crawl,
    /// <summary>The page looks like a deterministic hidden-form relay flow.</summary>
    BrowserlessRelayCandidate,
    /// <summary>The page appears to require an interactive or credentialed login flow.</summary>
    AuthRequired
}
