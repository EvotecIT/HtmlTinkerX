using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    /// <summary>
    /// Saves a crawl result and any referenced page content to disk.
    /// </summary>
    /// <param name="result">Crawl result to save.</param>
    /// <param name="path">Directory or manifest path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task SaveResultAsync(HtmlCrawlResult result, string path, CancellationToken cancellationToken = default) =>
        PersistSnapshotAsync(result ?? throw new ArgumentNullException(nameof(result)), path, result.PendingPages, cancellationToken, null);

    /// <summary>
    /// Loads a previously saved crawl result from disk.
    /// </summary>
    /// <param name="path">Directory or manifest path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized crawl result.</returns>
    public static async Task<HtmlCrawlResult> LoadResultAsync(string path, CancellationToken cancellationToken = default) {
        if (path == null) {
            throw new ArgumentNullException(nameof(path));
        }

        string manifestPath = ResolveManifestPath(path);
        if (!File.Exists(manifestPath)) {
            throw new FileNotFoundException($"Crawl manifest not found: {manifestPath}", manifestPath);
        }

        string json;
#if NETSTANDARD2_0 || NETFRAMEWORK
        json = await Task.Run(() => File.ReadAllText(manifestPath), cancellationToken).ConfigureAwait(false);
#else
        json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
#endif

        JsonSerializerOptions options = CreateJsonOptions();
        HtmlCrawlResult? result = JsonSerializer.Deserialize<HtmlCrawlResult>(json, options);
        if (result == null) {
            throw new InvalidOperationException($"Unable to deserialize crawl result from '{manifestPath}'.");
        }

        return result;
    }

    private static async Task PersistSnapshotAsync(
        HtmlCrawlResult result,
        string path,
        Queue<CrawlRequest> pending,
        CancellationToken cancellationToken,
        HtmlCrawlOptions? options) =>
        await PersistSnapshotAsync(result, path, SnapshotPendingPages(pending), cancellationToken, options).ConfigureAwait(false);

    private static async Task PersistSnapshotAsync(
        HtmlCrawlResult result,
        string path,
        IEnumerable<HtmlCrawlPendingItem> pendingItems,
        CancellationToken cancellationToken,
        HtmlCrawlOptions? options) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        CrawlArtifactPaths artifactPaths = ResolveArtifactPaths(path);
        result.PendingPages = pendingItems.ToList();
        result.ManifestPath = artifactPaths.ManifestPath;
        result.PagesDirectoryPath = artifactPaths.PagesDirectory;
        result.AssetsDirectoryPath = artifactPaths.AssetsDirectory;
        result.PagesJsonlPath = artifactPaths.PagesJsonlPath;
        result.PagesCsvPath = artifactPaths.PagesCsvPath;
        result.SkippedPagesJsonlPath = artifactPaths.SkippedPagesJsonlPath;
        result.SkippedAssetsJsonlPath = artifactPaths.SkippedAssetsJsonlPath;
        result.LinksJsonlPath = artifactPaths.LinksJsonlPath;
        result.AssetsJsonlPath = artifactPaths.AssetsJsonlPath;
        result.StructuredJsonPagesJsonlPath = artifactPaths.StructuredJsonPagesJsonlPath;
        result.OpenApiLikePath = artifactPaths.OpenApiLikeJsonPath;
        result.OpenApiPath = artifactPaths.OpenApiJsonPath;
        result.ChunksJsonlPath = artifactPaths.ChunksJsonlPath;
        result.GraphJsonPath = artifactPaths.GraphJsonPath;
        result.SummaryPath = artifactPaths.SummaryJsonPath;
        result.SummaryTextPath = artifactPaths.SummaryTextPath;
        result.IndexHtmlPath = artifactPaths.IndexHtmlPath;
        UpdateDerivedResultData(result);

        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];
            string prefix = (i + 1).ToString("D4");
            string slug = BuildPageSlug(page, prefix);

            if (!string.IsNullOrEmpty(page.Html)) {
                page.HtmlPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.html");
            }

            if (!string.IsNullOrEmpty(page.Text)) {
                page.TextPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.txt");
            }

            if (!string.IsNullOrEmpty(page.Markdown)) {
                page.MarkdownPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.md");
            }

            if (page.StructuredJson != null) {
                page.StructuredJsonPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.structured.json");
            }

            page.ManifestPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.json");
        }

        Dictionary<string, string> localPageMap = BuildLocalPageMap(result.Pages);
        Dictionary<string, string> assetMap = result.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Url) && !string.IsNullOrWhiteSpace(asset.FilePath) && string.IsNullOrWhiteSpace(asset.Error))
            .GroupBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().FilePath!, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];

            if (!string.IsNullOrEmpty(page.Html)) {
                string htmlToWrite = ShouldRewriteStoredHtml(options)
                    ? RewriteStoredHtmlToLocalPaths(page.Html, page.Url, page.HtmlPath!, result.Assets, localPageMap, options!)
                    : page.Html;
                await WriteTextAsync(page.HtmlPath!, htmlToWrite, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(page.Text)) {
                await WriteTextAsync(page.TextPath!, page.Text, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(page.Markdown)) {
                await WriteTextAsync(page.MarkdownPath!, page.Markdown, cancellationToken).ConfigureAwait(false);
            }

            if (page.StructuredJson != null) {
                await WriteTextAsync(page.StructuredJsonPath!, JsonSerializer.Serialize(page.StructuredJson, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
            }

            await WriteTextAsync(page.ManifestPath!, BuildPageManifestJson(page, result.Assets, localPageMap, assetMap), cancellationToken).ConfigureAwait(false);
        }

        await RewriteDownloadedCssAssetsAsync(result.Assets, options, cancellationToken).ConfigureAwait(false);

        StringBuilder pagesJsonl = new();
        StringBuilder pagesCsv = new();
        pagesCsv.AppendLine("Url,RequestedUrl,CanonicalUrl,ParentUrl,Depth,Status,StatusCode,ContentType,Title,HtmlPath,TextPath,MarkdownPath,StructuredJsonPath,ManifestPath,ContentFingerprint,DuplicateOfUrl,Rendered,RenderMode,RenderReasonCode,RenderReason,AppliedScenario,AppliedProfileName,AppliedProfileReasonCode,AppliedProfileReason,ContentModeUsed,ContentSelectionReasonCode,ContentSelectionReason,ContentElementTag,ContentElementId,ContentElementClasses,ContentElementSelectorHint,ContentSelectionScore,ReaderCandidateCount,ReaderRootElementSelectorHint,ContentComparisonCount,BestContentComparisonMode,BestContentComparisonReasonCode,BestContentComparisonWordCount,RunnerUpContentComparisonMode,BestContentComparisonWordDelta,ContentComparisonDeltaSummary,ContentComparisonPreviewSummary,Started,Finished,DurationMs,LinkCount,AssetCount,InteractionCount,StructuredTableCount,StructuredListCount,StructuredFormCount,StructuredMicrodataCount,StructuredMetaTagCount,StructuredCodeBlockCount,StructuredCodeSampleCount,StructuredApiEndpointCount,StructuredAuthenticatedApiEndpointCount,StructuredRateLimitedApiEndpointCount,StructuredApiErrorResponseCount,StructuredBreadcrumbCount,StructuredFaqCount,StructuredSpecTableCount,StructuredCalloutCount,StructuredPrimaryActionCount,StructuredHeaderCount,StructuredNavigationCount,StructuredMainCount,StructuredArticleCount,StructuredAsideCount,StructuredFooterCount,OfflineReadinessGrade,HighestOfflineRiskSeverity,OfflineDependencyDiagnosticCount,OfflineDependencyKindsSummary,Error");
        foreach (HtmlCrawlPage page in result.Pages) {
            cancellationToken.ThrowIfCancellationRequested();
            pagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.StatusCode,
                page.ContentType,
                page.Title,
                page.HtmlPath,
                page.TextPath,
                page.MarkdownPath,
                page.StructuredJsonPath,
                page.ManifestPath,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.Rendered,
                page.RenderMode,
                page.RenderReasonCode,
                page.RenderReason,
                page.AppliedScenario,
                page.AppliedProfileName,
                page.AppliedProfileReasonCode,
                page.AppliedProfileReason,
                page.ContentModeUsed,
                page.ContentSelectionReasonCode,
                page.ContentSelectionReason,
                page.ContentElementTag,
                page.ContentElementId,
                page.ContentElementClasses,
                page.ContentElementSelectorHint,
                page.ContentSelectionScore,
                page.ReaderCandidateCount,
                page.ReaderRootElementSelectorHint,
                ContentComparisonCount = page.ContentComparisons.Count,
                page.BestContentComparisonMode,
                page.BestContentComparisonReasonCode,
                page.BestContentComparisonWordCount,
                page.RunnerUpContentComparisonMode,
                page.BestContentComparisonWordDelta,
                page.ContentComparisonDeltaSummary,
                page.ContentComparisonPreviewSummary,
                page.AppliedInteractions,
                page.Started,
                page.Finished,
                DurationMs = (long)page.Duration.TotalMilliseconds,
                LinkCount = page.Links.Count,
                AssetCount = page.AssetUrls.Count,
                StructuredTableCount = page.StructuredJson?.Tables.Count ?? 0,
                StructuredListCount = page.StructuredJson?.Lists.Count ?? 0,
                StructuredFormCount = page.StructuredJson?.Forms.Count ?? 0,
                StructuredMicrodataCount = page.StructuredJson?.MicrodataItems.Count ?? 0,
                StructuredMetaTagCount = page.StructuredJson?.MetaTags.Count ?? 0,
                StructuredCodeBlockCount = page.StructuredJson?.CodeBlocks.Count ?? 0,
                StructuredCodeSampleCount = page.StructuredJson?.CodeSamples.Count ?? 0,
                StructuredApiEndpointCount = page.StructuredJson?.ApiEndpoints.Count ?? 0,
                StructuredAuthenticatedApiEndpointCount = GetStructuredAuthenticatedApiEndpointCount(page.StructuredJson),
                StructuredRateLimitedApiEndpointCount = GetStructuredRateLimitedApiEndpointCount(page.StructuredJson),
                StructuredApiErrorResponseCount = GetStructuredApiErrorResponseCount(page.StructuredJson),
                StructuredBreadcrumbCount = page.StructuredJson?.Breadcrumbs.Count ?? 0,
                StructuredFaqCount = page.StructuredJson?.FaqItems.Count ?? 0,
                StructuredSpecTableCount = page.StructuredJson?.SpecTables.Count ?? 0,
                StructuredCalloutCount = page.StructuredJson?.Callouts.Count ?? 0,
                StructuredPrimaryActionCount = page.StructuredJson?.PrimaryActions.Count ?? 0,
                StructuredHeaderCount = page.StructuredJson?.Layout.HeaderCount ?? 0,
                StructuredNavigationCount = page.StructuredJson?.Layout.NavigationCount ?? 0,
                StructuredMainCount = page.StructuredJson?.Layout.MainCount ?? 0,
                StructuredArticleCount = page.StructuredJson?.Layout.ArticleCount ?? 0,
                StructuredAsideCount = page.StructuredJson?.Layout.AsideCount ?? 0,
                StructuredFooterCount = page.StructuredJson?.Layout.FooterCount ?? 0,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKinds,
                page.OfflineDependencyKindsSummary,
                OfflineDependencyDiagnostics = page.OfflineDependencyDiagnostics,
                page.Error
            }));

            pagesCsv.AppendLine(string.Join(",",
                EscapeCsv(page.Url),
                EscapeCsv(page.RequestedUrl),
                EscapeCsv(page.CanonicalUrl),
                EscapeCsv(page.ParentUrl),
                EscapeCsv(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Status.ToString()),
                EscapeCsv(page.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ContentType),
                EscapeCsv(page.Title),
                EscapeCsv(page.HtmlPath),
                EscapeCsv(page.TextPath),
                EscapeCsv(page.MarkdownPath),
                EscapeCsv(page.StructuredJsonPath),
                EscapeCsv(page.ManifestPath),
                EscapeCsv(page.ContentFingerprint),
                EscapeCsv(page.DuplicateOfUrl),
                EscapeCsv(page.Rendered.ToString()),
                EscapeCsv(page.RenderMode.ToString()),
                EscapeCsv(page.RenderReasonCode.ToString()),
                EscapeCsv(page.RenderReason),
                EscapeCsv(page.AppliedScenario.ToString()),
                EscapeCsv(page.AppliedProfileName),
                EscapeCsv(page.AppliedProfileReasonCode.ToString()),
                EscapeCsv(page.AppliedProfileReason),
                EscapeCsv(page.ContentModeUsed.ToString()),
                EscapeCsv(page.ContentSelectionReasonCode.ToString()),
                EscapeCsv(page.ContentSelectionReason),
                EscapeCsv(page.ContentElementTag),
                EscapeCsv(page.ContentElementId),
                EscapeCsv(string.Join("|", page.ContentElementClasses)),
                EscapeCsv(page.ContentElementSelectorHint),
                EscapeCsv(page.ContentSelectionScore?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ReaderCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ReaderRootElementSelectorHint),
                EscapeCsv(page.ContentComparisons.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.BestContentComparisonMode?.ToString()),
                EscapeCsv(page.BestContentComparisonReasonCode?.ToString()),
                EscapeCsv(page.BestContentComparisonWordCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.RunnerUpContentComparisonMode?.ToString()),
                EscapeCsv(page.BestContentComparisonWordDelta?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ContentComparisonDeltaSummary),
                EscapeCsv(page.ContentComparisonPreviewSummary),
                EscapeCsv(page.Started.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Finished.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(((long)page.Duration.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Links.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AssetUrls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AppliedInteractions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Tables.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Lists.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Forms.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.MicrodataItems.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.MetaTags.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.CodeBlocks.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.CodeSamples.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.ApiEndpoints.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredAuthenticatedApiEndpointCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredRateLimitedApiEndpointCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredApiErrorResponseCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Breadcrumbs.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.FaqItems.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.SpecTables.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Callouts.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.PrimaryActions.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.HeaderCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.NavigationCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.MainCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.ArticleCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.AsideCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.FooterCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.OfflineReadinessGrade),
                EscapeCsv(page.HighestOfflineRiskSeverity),
                EscapeCsv(page.OfflineDependencyDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.OfflineDependencyKindsSummary),
                EscapeCsv(page.Error)));
        }

        List<HtmlCrawlPage> skippedContentPages = result.SkippedPages
            .Where(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath)
            .ToList();
        List<HtmlCrawlPage> skippedAssetPages = result.SkippedPages
            .Where(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath)
            .ToList();

        StringBuilder skippedPagesJsonl = new();
        foreach (HtmlCrawlPage page in skippedContentPages) {
            cancellationToken.ThrowIfCancellationRequested();
            skippedPagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.SkipReason,
                page.ContentType,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKindsSummary,
                page.Error
            }));
        }

        StringBuilder skippedAssetsJsonl = new();
        foreach (HtmlCrawlPage page in skippedAssetPages) {
            cancellationToken.ThrowIfCancellationRequested();
            skippedAssetsJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.SkipReason,
                page.ContentType,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKindsSummary,
                page.Error
            }));
        }

        StringBuilder linksJsonl = new();
        foreach (HtmlCrawlPage page in result.Pages.Where(page => !string.IsNullOrWhiteSpace(page.Url))) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string link in page.Links.Where(link => !string.IsNullOrWhiteSpace(link)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                linksJsonl.AppendLine(JsonSerializer.Serialize(new {
                    SourceUrl = page.Url,
                    TargetUrl = link,
                    page.Depth,
                    page.Rendered
                }));
            }
        }

        StringBuilder assetsJsonl = new();
        foreach (HtmlCrawlAsset asset in result.Assets) {
            cancellationToken.ThrowIfCancellationRequested();
            assetsJsonl.AppendLine(JsonSerializer.Serialize(new {
                asset.Url,
                asset.PageUrl,
                asset.Source,
                asset.ContentType,
                asset.StatusCode,
                asset.FilePath,
                asset.ContentLength,
                asset.Error,
                asset.Started,
                asset.Finished,
                DurationMs = (long)asset.Duration.TotalMilliseconds
            }));
        }

        StringBuilder structuredPagesJsonl = new();
        foreach (HtmlCrawlPage page in result.Pages.Where(page => page.StructuredJson != null)) {
            cancellationToken.ThrowIfCancellationRequested();
            structuredPagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.Title,
                page.Depth,
                page.StructuredJsonPath,
                page.StructuredJson
            }, CreateJsonOptions()));
        }

        List<PageChunkRecord> chunkRecords = BuildChunkRecords(result.Pages);
        result.ChunkCount = chunkRecords.Count;
        StringBuilder chunksJsonl = new();
        foreach (PageChunkRecord chunk in chunkRecords) {
            cancellationToken.ThrowIfCancellationRequested();
            chunksJsonl.AppendLine(JsonSerializer.Serialize(new {
                chunk.ChunkId,
                chunk.Url,
                chunk.Title,
                chunk.Depth,
                chunk.ChunkIndex,
                chunk.WordCount,
                chunk.CharacterCount,
                chunk.Summary,
                chunk.Headings,
                chunk.Keywords,
                chunk.Text,
                HtmlPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.HtmlPath),
                TextPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.TextPath),
                ManifestPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.ManifestPath),
                chunk.OfflineReadinessGrade,
                chunk.HighestOfflineRiskSeverity,
                chunk.OfflineDependencyDiagnosticCount,
                chunk.OfflineDependencyKindsSummary,
                chunk.Fingerprint
            }));
        }

        (object graphDocument, int graphNodeCount, int graphEdgeCount, int fetchedNodeCount, int skippedNodeCount, int externalNodeCount, Dictionary<string, int> nodeCategories, Dictionary<string, int> edgeRelations, Dictionary<string, int> skippedNodeReasons) =
            BuildGraphDocument(result.Pages, result.SkippedPages, artifactPaths.GraphJsonPath);
        result.GraphNodeCount = graphNodeCount;
        result.GraphEdgeCount = graphEdgeCount;
        result.GraphFetchedNodeCount = fetchedNodeCount;
        result.GraphSkippedNodeCount = skippedNodeCount;
        result.GraphExternalNodeCount = externalNodeCount;
        result.GraphNodeCategories = nodeCategories;
        result.GraphEdgeRelations = edgeRelations;
        result.GraphSkippedNodeReasons = skippedNodeReasons;

        HtmlCrawlSummary summary = result.Summary;
        await WriteTextAsync(artifactPaths.PagesJsonlPath, pagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.PagesCsvPath, pagesCsv.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SkippedPagesJsonlPath, skippedPagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SkippedAssetsJsonlPath, skippedAssetsJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.LinksJsonlPath, linksJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.AssetsJsonlPath, assetsJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.StructuredJsonPagesJsonlPath, structuredPagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.OpenApiLikeJsonPath, JsonSerializer.Serialize(result.OpenApiLike, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.OpenApiJsonPath, JsonSerializer.Serialize(result.OpenApiDocument, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.ChunksJsonlPath, chunksJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.GraphJsonPath, JsonSerializer.Serialize(graphDocument, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SummaryJsonPath, JsonSerializer.Serialize(summary, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SummaryTextPath, summary.ToReportText(result.SitemapUrls), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.IndexHtmlPath, BuildIndexHtml(result, summary, artifactPaths.IndexHtmlPath), cancellationToken).ConfigureAwait(false);

        string json = JsonSerializer.Serialize(result, CreateJsonOptions());
        await WriteTextAsync(artifactPaths.ManifestPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static List<HtmlCrawlPendingItem> SnapshotPendingPages(IEnumerable<CrawlRequest> pending) {
        List<HtmlCrawlPendingItem> snapshot = new();
        foreach (CrawlRequest item in pending) {
            snapshot.Add(new HtmlCrawlPendingItem {
                Url = item.Uri.AbsoluteUri,
                ParentUrl = item.ParentUrl,
                Depth = item.Depth
            });
        }

        return snapshot;
    }

    private static void UpdateDerivedResultData(HtmlCrawlResult result) {
        result.OpenApiLike = BuildResultOpenApiLike(result);
        result.OpenApiDocument = BuildResultOpenApiDocument(result.OpenApiLike, result);
    }

}
