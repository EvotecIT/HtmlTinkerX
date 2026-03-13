using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Represents the result of an offline crawl.
/// </summary>
public sealed class HtmlCrawlResult {
    /// <summary>Starting URL used for the crawl.</summary>
    public string StartUrl { get; set; } = string.Empty;

    /// <summary>Timestamp when the crawl started.</summary>
    public DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp when the crawl finished.</summary>
    public DateTimeOffset Finished { get; set; }

    /// <summary>Pages collected during the crawl.</summary>
    public IList<HtmlCrawlPage> Pages { get; set; } = new List<HtmlCrawlPage>();

    /// <summary>Assets downloaded from crawled pages.</summary>
    public IList<HtmlCrawlAsset> Assets { get; set; } = new List<HtmlCrawlAsset>();

    /// <summary>Candidates discovered but skipped by crawl rules.</summary>
    public IList<HtmlCrawlPage> SkippedPages { get; set; } = new List<HtmlCrawlPage>();

    /// <summary>Candidates still pending when the crawl snapshot was written.</summary>
    public IList<HtmlCrawlPendingItem> PendingPages { get; set; } = new List<HtmlCrawlPendingItem>();

    /// <summary>Sitemaps used to discover crawl candidates.</summary>
    public IList<string> SitemapUrls { get; set; } = new List<string>();

    /// <summary>Name of the crawl profile that was applied, when one was used.</summary>
    public string? AppliedProfileName { get; set; }

    /// <summary>Intent-focused scenario that was applied to the crawl before profiles and explicit options refined it.</summary>
    public HtmlCrawlScenario AppliedScenario { get; set; }

    /// <summary>Categorizes why the applied crawl profile was selected.</summary>
    public HtmlCrawlProfileSelectionReasonCode AppliedProfileReasonCode { get; set; }

    /// <summary>Explains why the applied crawl profile was selected.</summary>
    public string? AppliedProfileReason { get; set; }

    /// <summary>Manifest path used when crawl artifacts were persisted.</summary>
    public string? ManifestPath { get; set; }

    /// <summary>Directory containing persisted page content files.</summary>
    public string? PagesDirectoryPath { get; set; }

    /// <summary>Directory containing persisted downloaded assets.</summary>
    public string? AssetsDirectoryPath { get; set; }

    /// <summary>Path to the generated per-page JSONL dataset file.</summary>
    public string? PagesJsonlPath { get; set; }

    /// <summary>Path to the generated per-page CSV dataset file.</summary>
    public string? PagesCsvPath { get; set; }

    /// <summary>Path to the generated skipped-pages JSONL dataset file.</summary>
    public string? SkippedPagesJsonlPath { get; set; }

    /// <summary>Path to the generated skipped-assets JSONL dataset file.</summary>
    public string? SkippedAssetsJsonlPath { get; set; }

    /// <summary>Path to the generated discovered-links JSONL dataset file.</summary>
    public string? LinksJsonlPath { get; set; }

    /// <summary>Path to the generated downloaded-assets JSONL dataset file.</summary>
    public string? AssetsJsonlPath { get; set; }

    /// <summary>Path to the generated deduplicated text chunks JSONL dataset file.</summary>
    public string? ChunksJsonlPath { get; set; }

    /// <summary>Number of exported deduplicated text chunks.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Path to the generated link graph JSON file.</summary>
    public string? GraphJsonPath { get; set; }

    /// <summary>Number of exported graph nodes.</summary>
    public int GraphNodeCount { get; set; }

    /// <summary>Number of exported graph edges.</summary>
    public int GraphEdgeCount { get; set; }

    /// <summary>Number of fetched graph nodes.</summary>
    public int GraphFetchedNodeCount { get; set; }

    /// <summary>Number of skipped graph nodes.</summary>
    public int GraphSkippedNodeCount { get; set; }

    /// <summary>Number of external graph nodes.</summary>
    public int GraphExternalNodeCount { get; set; }

    /// <summary>Counts of graph nodes by category.</summary>
    public IDictionary<string, int> GraphNodeCategories { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of graph edges by relation.</summary>
    public IDictionary<string, int> GraphEdgeRelations { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of skipped graph nodes by skip reason.</summary>
    public IDictionary<string, int> GraphSkippedNodeReasons { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Path to the generated summary JSON file.</summary>
    public string? SummaryPath { get; set; }

    /// <summary>Path to the generated human-readable summary report.</summary>
    public string? SummaryTextPath { get; set; }

    /// <summary>Path to the generated browsable HTML index report.</summary>
    public string? IndexHtmlPath { get; set; }

    /// <summary>Total crawl duration.</summary>
    public TimeSpan Duration => Finished - Started;

    /// <summary>Total page count.</summary>
    public int PageCount => Pages.Count;

    /// <summary>Number of failed pages.</summary>
    public int FailedPageCount => Pages.Count(page => !string.IsNullOrEmpty(page.Error));

    /// <summary>Number of downloaded assets.</summary>
    public int AssetCount => Assets.Count;

    /// <summary>Number of skipped pages.</summary>
    public int SkippedPageCount => SkippedPages.Count;

    /// <summary>Number of skipped content-page candidates.</summary>
    public int SkippedContentPageCount => SkippedPages.Count(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath);

    /// <summary>Number of skipped asset candidates.</summary>
    public int SkippedAssetCount => SkippedPages.Count(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath);

    /// <summary>Summary information for the crawl.</summary>
    public HtmlCrawlSummary Summary => HtmlCrawlSummary.FromResult(this);
}
