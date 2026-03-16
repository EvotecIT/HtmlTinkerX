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

    /// <summary>Name of the crawl profile that was applied, when one was used.</summary>
    public string? AppliedProfileName { get; set; }

    /// <summary>Intent-focused scenario that was applied to the crawl before profiles and explicit options refined it.</summary>
    public HtmlCrawlScenario AppliedScenario { get; set; }

    /// <summary>Categorizes why the applied crawl profile was selected.</summary>
    public HtmlCrawlProfileSelectionReasonCode AppliedProfileReasonCode { get; set; }

    /// <summary>Explains why the applied crawl profile was selected.</summary>
    public string? AppliedProfileReason { get; set; }

    /// <summary>Total successful or failed fetches recorded in <see cref="HtmlCrawlResult.Pages"/>.</summary>
    public int PageCount { get; set; }

    /// <summary>Number of successful pages.</summary>
    public int SuccessfulPageCount { get; set; }

    /// <summary>Number of failed pages.</summary>
    public int FailedPageCount { get; set; }

    /// <summary>Number of rendered pages.</summary>
    public int RenderedPageCount { get; set; }

    /// <summary>Number of pages rendered only after auto-render fallback heuristics triggered.</summary>
    public int AutoRenderedPageCount { get; set; }

    /// <summary>Number of skipped candidates.</summary>
    public int SkippedPageCount { get; set; }

    /// <summary>Number of skipped content-page candidates.</summary>
    public int SkippedContentPageCount { get; set; }

    /// <summary>Number of skipped asset candidates.</summary>
    public int SkippedAssetCount { get; set; }

    /// <summary>Number of skipped candidates that duplicated earlier content.</summary>
    public int DuplicatePageCount { get; set; }

    /// <summary>Number of pending candidates.</summary>
    public int PendingPageCount { get; set; }

    /// <summary>Number of sitemap sources used during the crawl.</summary>
    public int SitemapCount { get; set; }

    /// <summary>Number of downloaded assets.</summary>
    public int AssetCount { get; set; }

    /// <summary>Number of pages that include structured extracted data.</summary>
    public int StructuredPageCount { get; set; }

    /// <summary>Total structured code blocks captured across fetched pages.</summary>
    public int StructuredCodeBlockCount { get; set; }

    /// <summary>Total structured code samples captured across fetched pages.</summary>
    public int StructuredCodeSampleCount { get; set; }

    /// <summary>Total structured API endpoints captured across fetched pages.</summary>
    public int StructuredApiEndpointCount { get; set; }

    /// <summary>Total distinct structured API paths merged across fetched pages.</summary>
    public int StructuredApiPathCount { get; set; }

    /// <summary>Total distinct structured API resources merged across fetched pages.</summary>
    public int StructuredApiResourceCount { get; set; }

    /// <summary>Total structured API operations strong enough to be promoted into the strict OpenAPI export.</summary>
    public int StructuredOpenApiPromotedOperationCount { get; set; }

    /// <summary>Total structured API operations retained only in the OpenAPI-like export because they were too weak for strict promotion.</summary>
    public int StructuredOpenApiSkippedOperationCount { get; set; }

    /// <summary>Average promotion score across inferred structured API operations.</summary>
    public double StructuredOpenApiAveragePromotionScore { get; set; }

    /// <summary>Total reusable structured API schema components merged across fetched pages.</summary>
    public int StructuredApiSchemaComponentCount { get; set; }

    /// <summary>Total reusable structured API field-set components merged across fetched pages.</summary>
    public int StructuredApiFieldSetComponentCount { get; set; }

    /// <summary>Total reusable structured API authentication profile components merged across fetched pages.</summary>
    public int StructuredApiAuthProfileCount { get; set; }

    /// <summary>Total reusable structured API rate-limit profile components merged across fetched pages.</summary>
    public int StructuredApiRateLimitProfileCount { get; set; }

    /// <summary>Total reusable structured API parameter-set components merged across fetched pages.</summary>
    public int StructuredApiParameterSetCount { get; set; }

    /// <summary>Total reusable structured API header-set components merged across fetched pages.</summary>
    public int StructuredApiHeaderSetCount { get; set; }

    /// <summary>Total reusable structured API example-set components merged across fetched pages.</summary>
    public int StructuredApiExampleSetCount { get; set; }

    /// <summary>Total reusable structured API error-catalog components merged across fetched pages.</summary>
    public int StructuredApiErrorCatalogComponentCount { get; set; }

    /// <summary>Total structured API endpoints with authentication hints across fetched pages.</summary>
    public int StructuredAuthenticatedApiEndpointCount { get; set; }

    /// <summary>Total structured API endpoints with rate-limit hints across fetched pages.</summary>
    public int StructuredRateLimitedApiEndpointCount { get; set; }

    /// <summary>Total structured API error responses captured across fetched pages.</summary>
    public int StructuredApiErrorResponseCount { get; set; }

    /// <summary>Total structured breadcrumb trails captured across fetched pages.</summary>
    public int StructuredBreadcrumbCount { get; set; }

    /// <summary>Total structured FAQ items captured across fetched pages.</summary>
    public int StructuredFaqCount { get; set; }

    /// <summary>Total structured specification tables captured across fetched pages.</summary>
    public int StructuredSpecTableCount { get; set; }

    /// <summary>Total structured callouts captured across fetched pages.</summary>
    public int StructuredCalloutCount { get; set; }

    /// <summary>Total structured primary actions captured across fetched pages.</summary>
    public int StructuredPrimaryActionCount { get; set; }

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

    /// <summary>Counts of fetched pages by render mode.</summary>
    public IDictionary<string, int> RenderModeCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of fetched pages by render-reason category.</summary>
    public IDictionary<string, int> RenderReasonCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of successful pages by content-selection mode.</summary>
    public IDictionary<string, int> ContentModeCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of successful pages by content-selection reason category.</summary>
    public IDictionary<string, int> ContentSelectionCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts of pages by the strongest extraction mode chosen from content comparisons.</summary>
    public IDictionary<string, int> ContentComparisonWinnerCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Representative comparison preview samples grouped by the strongest extraction mode chosen from content comparisons.</summary>
    public IDictionary<string, string> ContentComparisonWinnerPreviewSamples { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Average word-count delta between the best and runner-up extraction modes when comparison mode is enabled.</summary>
    public double AverageBestContentComparisonWordDelta { get; set; }

    /// <summary>Number of fetched pages where one or more rendered interactions were applied.</summary>
    public int InteractedPageCount { get; set; }

    /// <summary>Total number of rendered interactions applied across fetched pages.</summary>
    public int InteractionCount { get; set; }

    /// <summary>Counts of applied rendered interactions by label.</summary>
    public IDictionary<string, int> InteractionCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
            AppliedProfileName = result.AppliedProfileName,
            AppliedScenario = result.AppliedScenario,
            AppliedProfileReasonCode = result.AppliedProfileReasonCode,
            AppliedProfileReason = result.AppliedProfileReason,
            PageCount = result.Pages.Count,
            SuccessfulPageCount = result.Pages.Count(page => page.Status == HtmlCrawlPageStatus.Success),
            FailedPageCount = result.Pages.Count(page => page.Status == HtmlCrawlPageStatus.Failed),
            RenderedPageCount = result.Pages.Count(page => page.Rendered),
            AutoRenderedPageCount = result.Pages.Count(page => page.RenderMode == HtmlCrawlRenderMode.AutoRendered),
            SkippedPageCount = result.SkippedPages.Count,
            SkippedContentPageCount = result.SkippedPages.Count(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath),
            SkippedAssetCount = result.SkippedPages.Count(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath),
            DuplicatePageCount = result.SkippedPages.Count(page => page.SkipReason == HtmlCrawlSkipReason.DuplicateContent),
            PendingPageCount = result.PendingPages.Count,
            SitemapCount = result.SitemapUrls.Count,
            AssetCount = result.Assets.Count,
            StructuredPageCount = result.Pages.Count(page => page.StructuredJson != null),
            StructuredCodeBlockCount = result.Pages.Sum(page => page.StructuredJson?.CodeBlocks.Count ?? 0),
            StructuredCodeSampleCount = result.Pages.Sum(page => page.StructuredJson?.CodeSamples.Count ?? 0),
            StructuredApiEndpointCount = result.Pages.Sum(page => page.StructuredJson?.ApiEndpoints.Count ?? 0),
            StructuredApiPathCount = result.OpenApiLike.Paths.Count,
            StructuredApiResourceCount = result.OpenApiLike.Resources.Count,
            StructuredOpenApiPromotedOperationCount = result.OpenApiLike.StrictOpenApiEligibleOperationCount,
            StructuredOpenApiSkippedOperationCount = result.OpenApiLike.StrictOpenApiSkippedOperationCount,
            StructuredOpenApiAveragePromotionScore = result.OpenApiLike.StrictOpenApiAverageScore,
            StructuredApiSchemaComponentCount = result.OpenApiLike.Components.Schemas.Count,
            StructuredApiFieldSetComponentCount = result.OpenApiLike.Components.FieldSets.Count,
            StructuredApiAuthProfileCount = result.OpenApiLike.Components.AuthProfiles.Count,
            StructuredApiRateLimitProfileCount = result.OpenApiLike.Components.RateLimitProfiles.Count,
            StructuredApiParameterSetCount = result.OpenApiLike.Components.ParameterSets.Count,
            StructuredApiHeaderSetCount = result.OpenApiLike.Components.RequestHeaderSets.Count + result.OpenApiLike.Components.ResponseHeaderSets.Count,
            StructuredApiExampleSetCount = result.OpenApiLike.Components.RequestExampleSets.Count + result.OpenApiLike.Components.ResponseExampleSets.Count,
            StructuredApiErrorCatalogComponentCount = result.OpenApiLike.Components.ErrorCatalogs.Count,
            StructuredAuthenticatedApiEndpointCount = result.Pages.Sum(page => HtmlCrawler.GetStructuredAuthenticatedApiEndpointCount(page.StructuredJson)),
            StructuredRateLimitedApiEndpointCount = result.Pages.Sum(page => HtmlCrawler.GetStructuredRateLimitedApiEndpointCount(page.StructuredJson)),
            StructuredApiErrorResponseCount = result.Pages.Sum(page => HtmlCrawler.GetStructuredApiErrorResponseCount(page.StructuredJson)),
            StructuredBreadcrumbCount = result.Pages.Sum(page => page.StructuredJson?.Breadcrumbs.Count ?? 0),
            StructuredFaqCount = result.Pages.Sum(page => page.StructuredJson?.FaqItems.Count ?? 0),
            StructuredSpecTableCount = result.Pages.Sum(page => page.StructuredJson?.SpecTables.Count ?? 0),
            StructuredCalloutCount = result.Pages.Sum(page => page.StructuredJson?.Callouts.Count ?? 0),
            StructuredPrimaryActionCount = result.Pages.Sum(page => page.StructuredJson?.PrimaryActions.Count ?? 0),
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
        summary.InteractedPageCount = result.Pages.Count(page => page.AppliedInteractions.Count > 0);
        summary.InteractionCount = result.Pages.Sum(page => page.AppliedInteractions.Count);
        foreach (var group in result.Pages.GroupBy(page => page.RenderMode.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.RenderModeCounts[group.Key] = group.Count();
        }
        foreach (var group in result.Pages.GroupBy(page => page.RenderReasonCode.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.RenderReasonCounts[group.Key] = group.Count();
        }
        foreach (var group in result.Pages.Where(page => page.Status == HtmlCrawlPageStatus.Success).GroupBy(page => page.ContentModeUsed.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.ContentModeCounts[group.Key] = group.Count();
        }
        foreach (var group in result.Pages.Where(page => page.Status == HtmlCrawlPageStatus.Success).GroupBy(page => page.ContentSelectionReasonCode.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.ContentSelectionCounts[group.Key] = group.Count();
        }
        foreach (var group in result.Pages
            .Where(page => page.Status == HtmlCrawlPageStatus.Success && page.BestContentComparisonMode.HasValue)
            .GroupBy(page => page.BestContentComparisonMode!.Value.ToString(), StringComparer.OrdinalIgnoreCase)) {
            summary.ContentComparisonWinnerCounts[group.Key] = group.Count();
            HtmlCrawlPage? representativePage = group
                .Where(page => !string.IsNullOrWhiteSpace(page.ContentComparisonPreviewSummary))
                .OrderByDescending(page => page.BestContentComparisonWordCount ?? 0)
                .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            string previewSample = representativePage?.ContentComparisonPreviewSummary ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(previewSample)) {
                summary.ContentComparisonWinnerPreviewSamples[group.Key] = previewSample;
            }
        }
        double[] comparisonDeltas = result.Pages
            .Where(page => page.Status == HtmlCrawlPageStatus.Success && page.BestContentComparisonWordDelta.HasValue)
            .Select(page => (double)page.BestContentComparisonWordDelta!.Value)
            .ToArray();
        summary.AverageBestContentComparisonWordDelta = comparisonDeltas.Length == 0 ? 0 : comparisonDeltas.Average();
        foreach (var group in result.Pages.SelectMany(page => page.AppliedInteractions).GroupBy(item => item, StringComparer.OrdinalIgnoreCase)) {
            summary.InteractionCounts[group.Key] = group.Count();
        }
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
        if (AppliedScenario != HtmlCrawlScenario.Custom) {
            report.AppendLine($"Scenario: {AppliedScenario}");
        }
        if (!string.IsNullOrWhiteSpace(AppliedProfileName)) {
            report.AppendLine($"Profile: {AppliedProfileName}");
            if (AppliedProfileReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                report.AppendLine($"Profile reason code: {AppliedProfileReasonCode}");
            }
            if (!string.IsNullOrWhiteSpace(AppliedProfileReason)) {
                report.AppendLine($"Profile reason: {AppliedProfileReason}");
            }
        }
        report.AppendLine($"Pages fetched: {PageCount}");
        report.AppendLine($"Successful pages: {SuccessfulPageCount}");
        report.AppendLine($"Failed pages: {FailedPageCount}");
        report.AppendLine($"Rendered pages: {RenderedPageCount}");
        report.AppendLine($"Auto-rendered pages: {AutoRenderedPageCount}");
        report.AppendLine($"Skipped candidates: {SkippedPageCount}");
        report.AppendLine($"Skipped page candidates: {SkippedContentPageCount}");
        report.AppendLine($"Skipped asset candidates: {SkippedAssetCount}");
        report.AppendLine($"Duplicate-content skips: {DuplicatePageCount}");
        report.AppendLine($"Pending candidates: {PendingPageCount}");
        report.AppendLine($"Sitemaps used: {SitemapCount}");
        report.AppendLine($"Downloaded assets: {AssetCount}");
        report.AppendLine($"Structured pages: {StructuredPageCount}");
        report.AppendLine($"Structured code blocks: {StructuredCodeBlockCount}");
        report.AppendLine($"Structured code samples: {StructuredCodeSampleCount}");
        report.AppendLine($"Structured API endpoints: {StructuredApiEndpointCount}");
        report.AppendLine($"Structured API paths: {StructuredApiPathCount}");
        report.AppendLine($"Structured API resources: {StructuredApiResourceCount}");
        report.AppendLine($"Structured OpenAPI promoted operations: {StructuredOpenApiPromotedOperationCount}");
        report.AppendLine($"Structured OpenAPI skipped operations: {StructuredOpenApiSkippedOperationCount}");
        report.AppendLine($"Structured OpenAPI average promotion score: {StructuredOpenApiAveragePromotionScore:0.##}");
        report.AppendLine($"Structured API schema components: {StructuredApiSchemaComponentCount}");
        report.AppendLine($"Structured API field-set components: {StructuredApiFieldSetComponentCount}");
        report.AppendLine($"Structured API auth profiles: {StructuredApiAuthProfileCount}");
        report.AppendLine($"Structured API rate-limit profiles: {StructuredApiRateLimitProfileCount}");
        report.AppendLine($"Structured API parameter sets: {StructuredApiParameterSetCount}");
        report.AppendLine($"Structured API header sets: {StructuredApiHeaderSetCount}");
        report.AppendLine($"Structured API example sets: {StructuredApiExampleSetCount}");
        report.AppendLine($"Structured API error catalogs: {StructuredApiErrorCatalogComponentCount}");
        report.AppendLine($"Structured authenticated endpoints: {StructuredAuthenticatedApiEndpointCount}");
        report.AppendLine($"Structured rate-limited endpoints: {StructuredRateLimitedApiEndpointCount}");
        report.AppendLine($"Structured API error responses: {StructuredApiErrorResponseCount}");
        report.AppendLine($"Structured breadcrumbs: {StructuredBreadcrumbCount}");
        report.AppendLine($"Structured FAQ items: {StructuredFaqCount}");
        report.AppendLine($"Structured spec tables: {StructuredSpecTableCount}");
        report.AppendLine($"Structured callouts: {StructuredCalloutCount}");
        report.AppendLine($"Structured primary actions: {StructuredPrimaryActionCount}");
        report.AppendLine($"Exported chunks: {ChunkCount}");
        report.AppendLine($"Graph nodes: {GraphNodeCount}");
        report.AppendLine($"Graph edges: {GraphEdgeCount}");
        report.AppendLine($"Fetched graph nodes: {GraphFetchedNodeCount}");
        report.AppendLine($"Skipped graph nodes: {GraphSkippedNodeCount}");
        report.AppendLine($"External graph nodes: {GraphExternalNodeCount}");
        report.AppendLine($"Discovered links: {TotalDiscoveredLinks}");
        report.AppendLine($"Pages with interactions: {InteractedPageCount}");
        report.AppendLine($"Applied interactions: {InteractionCount}");
        report.AppendLine($"Duration: {DurationMs} ms");

        if (RenderModeCounts.Count > 0 || RenderReasonCounts.Count > 0) {
            report.AppendLine();
            report.AppendLine("Render summary:");
            foreach (var item in RenderModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Render mode {item.Key}: {item.Value}");
            }
            foreach (var item in RenderReasonCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Render reason {item.Key}: {item.Value}");
            }
        }

        if (ContentModeCounts.Count > 0 || ContentSelectionCounts.Count > 0 || ContentComparisonWinnerCounts.Count > 0 || ContentComparisonWinnerPreviewSamples.Count > 0) {
            report.AppendLine();
            report.AppendLine("Content summary:");
            foreach (var item in ContentModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Content mode {item.Key}: {item.Value}");
            }
            foreach (var item in ContentSelectionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Content selection {item.Key}: {item.Value}");
            }
            foreach (var item in ContentComparisonWinnerCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Best comparison mode {item.Key}: {item.Value}");
            }
            if (AverageBestContentComparisonWordDelta > 0) {
                report.AppendLine($"- Average best-comparison delta: {AverageBestContentComparisonWordDelta.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} words");
            }
            foreach (var item in ContentComparisonWinnerPreviewSamples.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- Best comparison sample {item.Key}: {item.Value}");
            }
        }

        if (InteractionCounts.Count > 0) {
            report.AppendLine();
            report.AppendLine("Interaction summary:");
            foreach (var item in InteractionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                report.AppendLine($"- {item.Key}: {item.Value}");
            }
        }

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
