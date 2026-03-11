using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HtmlTinkerX;

/// <summary>
/// Summarizes a crawl result for quick reporting and dataset metadata.
/// </summary>
public sealed class HtmlCrawlSummary {
    /// <summary>Starting URL used for the crawl.</summary>
    public string StartUrl { get; set; } = string.Empty;

    /// <summary>Total successful or failed fetches recorded in <see cref="HtmlCrawlResult.Pages"/>.</summary>
    public int PageCount { get; set; }

    /// <summary>Number of successful pages.</summary>
    public int SuccessfulPageCount { get; set; }

    /// <summary>Number of failed pages.</summary>
    public int FailedPageCount { get; set; }

    /// <summary>Number of rendered pages.</summary>
    public int RenderedPageCount { get; set; }

    /// <summary>Number of skipped candidates.</summary>
    public int SkippedPageCount { get; set; }

    /// <summary>Number of skipped candidates that duplicated earlier content.</summary>
    public int DuplicatePageCount { get; set; }

    /// <summary>Number of pending candidates.</summary>
    public int PendingPageCount { get; set; }

    /// <summary>Number of sitemap sources used during the crawl.</summary>
    public int SitemapCount { get; set; }

    /// <summary>Number of downloaded assets.</summary>
    public int AssetCount { get; set; }

    /// <summary>Number of exported text chunks.</summary>
    public int ChunkCount { get; set; }

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

    /// <summary>Duration of the crawl in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Total discovered outbound links across fetched pages.</summary>
    public int TotalDiscoveredLinks { get; set; }

    /// <summary>Counts of skipped pages by reason.</summary>
    public IDictionary<string, int> SkipReasons { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of fetched pages by HTTP status code.</summary>
    public IDictionary<string, int> StatusCodes { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    internal static HtmlCrawlSummary FromResult(HtmlCrawlResult result) {
        var summary = new HtmlCrawlSummary {
            StartUrl = result.StartUrl,
            PageCount = result.Pages.Count,
            SuccessfulPageCount = result.Pages.Count(page => page.Status == HtmlCrawlPageStatus.Success),
            FailedPageCount = result.Pages.Count(page => page.Status == HtmlCrawlPageStatus.Failed),
            RenderedPageCount = result.Pages.Count(page => page.Rendered),
            SkippedPageCount = result.SkippedPages.Count,
            DuplicatePageCount = result.SkippedPages.Count(page => page.SkipReason == HtmlCrawlSkipReason.DuplicateContent),
            PendingPageCount = result.PendingPages.Count,
            SitemapCount = result.SitemapUrls.Count,
            AssetCount = result.Assets.Count,
            DurationMs = result.Finished > result.Started ? (long)(result.Finished - result.Started).TotalMilliseconds : 0,
            TotalDiscoveredLinks = result.Pages.Sum(page => page.Links?.Count ?? 0)
        };

        summary.ChunkCount = result.ChunkCount > 0
            ? result.ChunkCount
            : HtmlCrawler.GetChunkCountForSummary(result.Pages);
        summary.GraphNodeCount = result.GraphNodeCount;
        summary.GraphEdgeCount = result.GraphEdgeCount;
        summary.GraphFetchedNodeCount = result.GraphFetchedNodeCount;
        summary.GraphSkippedNodeCount = result.GraphSkippedNodeCount;
        summary.GraphExternalNodeCount = result.GraphExternalNodeCount;
        foreach (var item in result.GraphNodeCategories) {
            summary.GraphNodeCategories[item.Key] = item.Value;
        }
        foreach (var item in result.GraphEdgeRelations) {
            summary.GraphEdgeRelations[item.Key] = item.Value;
        }
        foreach (var item in result.GraphSkippedNodeReasons) {
            summary.GraphSkippedNodeReasons[item.Key] = item.Value;
        }

        foreach (var group in result.SkippedPages.GroupBy(page => page.SkipReason.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.SkipReasons[group.Key] = group.Count();
        }

        foreach (var group in result.Pages.Where(page => page.StatusCode.HasValue).GroupBy(page => page.StatusCode!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)) {
            summary.StatusCodes[group.Key] = group.Count();
        }

        return summary;
    }

    /// <summary>
    /// Renders a human-readable crawl report.
    /// </summary>
    public string ToReportText(IEnumerable<string>? sitemapUrls = null) {
        StringBuilder report = new();
        report.AppendLine("=== HTML Crawl Summary ===");
        report.AppendLine($"Start URL: {StartUrl}");
        report.AppendLine($"Pages fetched: {PageCount}");
        report.AppendLine($"Successful pages: {SuccessfulPageCount}");
        report.AppendLine($"Failed pages: {FailedPageCount}");
        report.AppendLine($"Rendered pages: {RenderedPageCount}");
        report.AppendLine($"Skipped candidates: {SkippedPageCount}");
        report.AppendLine($"Duplicate-content skips: {DuplicatePageCount}");
        report.AppendLine($"Pending candidates: {PendingPageCount}");
        report.AppendLine($"Sitemaps used: {SitemapCount}");
        report.AppendLine($"Downloaded assets: {AssetCount}");
        report.AppendLine($"Exported chunks: {ChunkCount}");
        report.AppendLine($"Graph nodes: {GraphNodeCount}");
        report.AppendLine($"Graph edges: {GraphEdgeCount}");
        report.AppendLine($"Fetched graph nodes: {GraphFetchedNodeCount}");
        report.AppendLine($"Skipped graph nodes: {GraphSkippedNodeCount}");
        report.AppendLine($"External graph nodes: {GraphExternalNodeCount}");
        report.AppendLine($"Discovered links: {TotalDiscoveredLinks}");
        report.AppendLine($"Duration: {DurationMs} ms");

        if (GraphNodeCategories.Count > 0) {
            report.AppendLine();
            report.AppendLine("Graph node categories:");
            foreach (var item in GraphNodeCategories.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

        if (GraphEdgeRelations.Count > 0) {
            report.AppendLine();
            report.AppendLine("Graph edge relations:");
            foreach (var item in GraphEdgeRelations.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

        if (GraphSkippedNodeReasons.Count > 0) {
            report.AppendLine();
            report.AppendLine("Graph skipped-node reasons:");
            foreach (var item in GraphSkippedNodeReasons.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

        if (StatusCodes.Count > 0) {
            report.AppendLine();
            report.AppendLine("HTTP status codes:");
            foreach (var item in StatusCodes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

        if (SkipReasons.Count > 0) {
            report.AppendLine();
            report.AppendLine("Skip reasons:");
            foreach (var item in SkipReasons.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

        if (sitemapUrls != null) {
            string[] urls = sitemapUrls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (urls.Length > 0) {
                report.AppendLine();
                report.AppendLine("Sitemap sources:");
                foreach (string url in urls) {
                    report.AppendLine($"- {url}");
                }
            }
        }

        return report.ToString().TrimEnd();
    }
}
