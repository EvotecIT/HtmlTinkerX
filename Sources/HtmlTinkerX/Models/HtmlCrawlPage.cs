using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents one crawled page.
/// </summary>
public sealed class HtmlCrawlPage {
    /// <summary>Resolved page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Original URL requested before any canonical rewrite.</summary>
    public string? RequestedUrl { get; set; }

    /// <summary>URL that discovered this page.</summary>
    public string? ParentUrl { get; set; }

    /// <summary>Canonical URL discovered in the page markup, when available.</summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>Content fingerprint used for duplicate detection, when available.</summary>
    public string? ContentFingerprint { get; set; }

    /// <summary>Original page URL that this page duplicated, when duplicate detection skipped it.</summary>
    public string? DuplicateOfUrl { get; set; }

    /// <summary>Current crawl status for this candidate.</summary>
    public HtmlCrawlPageStatus Status { get; set; } = HtmlCrawlPageStatus.Success;

    /// <summary>Why the candidate was skipped, when applicable.</summary>
    public HtmlCrawlSkipReason SkipReason { get; set; }

    /// <summary>Distance from the starting URL.</summary>
    public int Depth { get; set; }

    /// <summary>HTTP status code when available.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Response content type when available.</summary>
    public string? ContentType { get; set; }

    /// <summary>Page title when available.</summary>
    public string? Title { get; set; }

    /// <summary>Stored HTML content.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>Optional persisted file path for the stored HTML.</summary>
    public string? HtmlPath { get; set; }

    /// <summary>Stored plain text content.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional persisted file path for the stored text.</summary>
    public string? TextPath { get; set; }

    /// <summary>Optional persisted file path for the page-level manifest sidecar.</summary>
    public string? ManifestPath { get; set; }

    /// <summary>Discovered outbound links after normalization.</summary>
    public IList<string> Links { get; set; } = new List<string>();

    /// <summary>Discovered asset URLs referenced from the page.</summary>
    public IList<string> AssetUrls { get; set; } = new List<string>();

    /// <summary>Rendered interactions that were successfully applied before extraction.</summary>
    public IList<string> AppliedInteractions { get; set; } = new List<string>();

    /// <summary>Error message when the page could not be fetched.</summary>
    public string? Error { get; set; }

    /// <summary>Indicates whether the page was rendered in a browser.</summary>
    public bool Rendered { get; set; }

    /// <summary>Describes whether the page stayed static, was rendered directly, or was auto-rendered after a static pass.</summary>
    public HtmlCrawlRenderMode RenderMode { get; set; } = HtmlCrawlRenderMode.Static;

    /// <summary>Explains why the selected render mode was used for this page.</summary>
    public string? RenderReason { get; set; }

    /// <summary>Categorizes why the selected render mode was used for this page.</summary>
    public HtmlCrawlRenderReasonCode RenderReasonCode { get; set; }

    /// <summary>Records which content-selection mode produced the stored HTML and text for this page.</summary>
    public HtmlCrawlContentMode ContentModeUsed { get; set; } = HtmlCrawlContentMode.Focused;

    /// <summary>Categorizes how the crawler chose the content region used for extraction.</summary>
    public HtmlCrawlContentSelectionReasonCode ContentSelectionReasonCode { get; set; }

    /// <summary>Explains how the crawler chose the content region used for extraction.</summary>
    public string? ContentSelectionReason { get; set; }

    /// <summary>Tag name of the selected content element when extraction targeted a specific node.</summary>
    public string? ContentElementTag { get; set; }

    /// <summary>Element ID of the selected content element when available.</summary>
    public string? ContentElementId { get; set; }

    /// <summary>Class names of the selected content element when available.</summary>
    public IList<string> ContentElementClasses { get; set; } = new List<string>();

    /// <summary>Compact selector-like hint for the selected content element when available.</summary>
    public string? ContentElementSelectorHint { get; set; }

    /// <summary>Score of the final selected content element when reader-mode scoring was used.</summary>
    public double? ContentSelectionScore { get; set; }

    /// <summary>Number of reader-mode candidate blocks that were evaluated for this page.</summary>
    public int ReaderCandidateCount { get; set; }

    /// <summary>Compact selector-like hint for the reader root element when reader-mode scoring was used.</summary>
    public string? ReaderRootElementSelectorHint { get; set; }

    /// <summary>Optional side-by-side extraction diagnostics for raw, focused, and reader modes.</summary>
    public IList<HtmlCrawlContentComparison> ContentComparisons { get; set; } = new List<HtmlCrawlContentComparison>();

    /// <summary>Timestamp when fetching started.</summary>
    public DateTimeOffset Started { get; set; }

    /// <summary>Timestamp when fetching finished.</summary>
    public DateTimeOffset Finished { get; set; }

    /// <summary>Total fetch duration.</summary>
    public TimeSpan Duration => Finished - Started;
}
