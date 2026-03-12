using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Defines a reusable set of crawl settings for a known site or site family.
/// </summary>
public sealed class HtmlCrawlProfile {
    /// <summary>Unique profile name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional host names that this profile can match automatically.</summary>
    public IList<string> Hosts { get; set; } = new List<string>();

    /// <summary>Optional focused selector applied when the caller does not set one explicitly.</summary>
    public string? Selector { get; set; }

    /// <summary>Optional content mode applied when the caller does not set one explicitly.</summary>
    public HtmlCrawlContentMode? ContentMode { get; set; }

    /// <summary>Optional minimum word count applied to reader-mode candidates when the caller keeps the default value.</summary>
    public int? ReaderMinimumWordCount { get; set; }

    /// <summary>Optional minimum score applied to reader-mode candidates when the caller keeps the default value.</summary>
    public double? ReaderMinimumScore { get; set; }

    /// <summary>Optional selector that rendered crawls wait for before extraction.</summary>
    public string? WaitForSelector { get; set; }

    /// <summary>Optional path prefix applied when the caller does not set one explicitly.</summary>
    public string? PathPrefix { get; set; }

    /// <summary>Enables auto-render fallback when the caller does not override it.</summary>
    public bool AutoRender { get; set; }

    /// <summary>Enables rendered auto-scroll when the caller does not override it.</summary>
    public bool AutoScroll { get; set; }

    /// <summary>Profile-specific repeat count for rendered interactions.</summary>
    public int? InteractionRepeatCount { get; set; }

    /// <summary>Profile-specific excluded selectors.</summary>
    public IList<string> ExcludeSelectors { get; set; } = new List<string>();

    /// <summary>Profile-specific excluded class names.</summary>
    public IList<string> ExcludeClasses { get; set; } = new List<string>();

    /// <summary>Profile-specific excluded element IDs.</summary>
    public IList<string> ExcludeIds { get; set; } = new List<string>();

    /// <summary>Profile-specific click selectors.</summary>
    public IList<string> ClickSelectors { get; set; } = new List<string>();

    /// <summary>Profile-specific click texts.</summary>
    public IList<string> ClickTexts { get; set; } = new List<string>();

    /// <summary>Profile-specific dismiss selectors.</summary>
    public IList<string> DismissSelectors { get; set; } = new List<string>();

    /// <summary>Profile-specific dismiss texts.</summary>
    public IList<string> DismissTexts { get; set; } = new List<string>();
}
