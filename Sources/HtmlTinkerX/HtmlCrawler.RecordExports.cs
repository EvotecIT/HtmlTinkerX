using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static Task ExportPageRecordsAsync(HtmlCrawlResult result, CrawlArtifactPaths paths, CancellationToken cancellationToken) =>
        WriteLinesAtomicallyAsync(paths.PagesCsvPath, pagesCsv =>
            WriteLinesAtomicallyAsync(paths.PagesJsonlPath, async pagesJsonl => {
                await pagesCsv.WriteLineAsync("Url,RequestedUrl,CanonicalUrl,ParentUrl,Depth,Status,StatusCode,ContentType,Title,HtmlPath,TextPath,MarkdownPath,StructuredJsonPath,ManifestPath,ContentFingerprint,DuplicateOfUrl,Rendered,RenderMode,RenderReasonCode,RenderReason,AppliedScenario,AppliedProfileName,AppliedProfileReasonCode,AppliedProfileReason,ContentModeUsed,ContentSelectionReasonCode,ContentSelectionReason,ContentElementTag,ContentElementId,ContentElementClasses,ContentElementSelectorHint,ContentSelectionScore,ReaderCandidateCount,ReaderRootElementSelectorHint,ContentComparisonCount,BestContentComparisonMode,BestContentComparisonReasonCode,BestContentComparisonWordCount,RunnerUpContentComparisonMode,BestContentComparisonWordDelta,ContentComparisonDeltaSummary,ContentComparisonPreviewSummary,Started,Finished,DurationMs,LinkCount,AssetCount,InteractionCount,StructuredTableCount,StructuredListCount,StructuredFormCount,StructuredMicrodataCount,StructuredMetaTagCount,StructuredCodeBlockCount,StructuredCodeSampleCount,StructuredApiEndpointCount,StructuredAuthenticatedApiEndpointCount,StructuredRateLimitedApiEndpointCount,StructuredApiErrorResponseCount,StructuredBreadcrumbCount,StructuredFaqCount,StructuredSpecTableCount,StructuredCalloutCount,StructuredPrimaryActionCount,StructuredHeaderCount,StructuredNavigationCount,StructuredMainCount,StructuredArticleCount,StructuredAsideCount,StructuredFooterCount,OfflineReadinessGrade,HighestOfflineRiskSeverity,OfflineDependencyDiagnosticCount,OfflineDependencyKindsSummary,Error,ResponseUrl,EntityTag,LastModified,ResponseContentHash,ResponseRevalidated,ResponseChanged");
                foreach (HtmlCrawlPage page in result.Pages) {
                    cancellationToken.ThrowIfCancellationRequested();
                    await pagesJsonl.WriteLineAsync(JsonSerializer.Serialize(new {
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
                        page.Error,
                        page.ResponseUrl,
                        page.EntityTag,
                        page.LastModified,
                        page.ResponseContentHash,
                        page.ResponseRevalidated,
                        page.ResponseChanged
                    }));

                    await pagesCsv.WriteLineAsync(string.Join(",",
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
                        EscapeCsv(page.Error),
                        EscapeCsv(page.ResponseUrl),
                        EscapeCsv(page.EntityTag),
                        EscapeCsv(page.LastModified?.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                        EscapeCsv(page.ResponseContentHash),
                        EscapeCsv(page.ResponseRevalidated.ToString()),
                        EscapeCsv(page.ResponseChanged?.ToString())));
                }
            }, cancellationToken), cancellationToken);

    private static Task ExportSkippedPageRecordsAsync(IEnumerable<HtmlCrawlPage> pages, string path, CancellationToken cancellationToken) =>
        WriteLinesAtomicallyAsync(path, async writer => {
            foreach (HtmlCrawlPage page in pages) {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonSerializer.Serialize(new {
                    page.Url,
                    page.RequestedUrl,
                    page.CanonicalUrl,
                    page.ParentUrl,
                    page.Depth,
                    page.Status,
                    page.SkipReason,
                    page.ContentType,
                    page.ResponseUrl,
                    page.EntityTag,
                    page.LastModified,
                    page.ResponseContentHash,
                    page.ResponseRevalidated,
                    page.ResponseChanged,
                    page.ContentFingerprint,
                    page.DuplicateOfUrl,
                    page.OfflineReadinessGrade,
                    page.HighestOfflineRiskSeverity,
                    page.OfflineDependencyDiagnosticCount,
                    page.OfflineDependencyKindsSummary,
                    page.Error
                }));
            }
        }, cancellationToken);

    private static Task ExportLinkRecordsAsync(HtmlCrawlResult result, string path, CancellationToken cancellationToken) =>
        WriteLinesAtomicallyAsync(path, async linksJsonl => {
            foreach (HtmlCrawlPage page in result.Pages.Where(page => !string.IsNullOrWhiteSpace(page.Url))) {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (string link in page.Links.Where(link => !string.IsNullOrWhiteSpace(link)).Distinct(StringComparer.Ordinal)) {
                    await linksJsonl.WriteLineAsync(JsonSerializer.Serialize(new {
                        SourceUrl = page.Url,
                        TargetUrl = link,
                        page.Depth,
                        page.Rendered
                    }));
                }
            }
        }, cancellationToken);

    private static Task ExportAssetRecordsAsync(HtmlCrawlResult result, string path, CancellationToken cancellationToken) =>
        WriteLinesAtomicallyAsync(path, async assetsJsonl => {
            foreach (HtmlCrawlAsset asset in result.Assets) {
                cancellationToken.ThrowIfCancellationRequested();
                await assetsJsonl.WriteLineAsync(JsonSerializer.Serialize(new {
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
        }, cancellationToken);

    private static Task ExportStructuredPageRecordsAsync(HtmlCrawlResult result, string path, CancellationToken cancellationToken) =>
        WriteLinesAtomicallyAsync(path, async structuredPagesJsonl => {
            foreach (HtmlCrawlPage page in result.Pages.Where(page => page.StructuredJson != null)) {
                cancellationToken.ThrowIfCancellationRequested();
                await structuredPagesJsonl.WriteLineAsync(JsonSerializer.Serialize(new {
                    page.Url,
                    page.Title,
                    page.Depth,
                    page.StructuredJsonPath,
                    page.StructuredJson
                }, CreateJsonOptions()));
            }
        }, cancellationToken);

    private static async Task<int> ExportChunkRecordsAsync(IEnumerable<PageChunkRecord> chunkRecords, string path, CancellationToken cancellationToken) {
        int count = 0;
        await WriteLinesAtomicallyAsync(path, async chunksJsonl => {
            foreach (PageChunkRecord chunk in chunkRecords) {
                cancellationToken.ThrowIfCancellationRequested();
                await chunksJsonl.WriteLineAsync(JsonSerializer.Serialize(new {
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
                    HtmlPath = BuildRelativeOptionalPath(path, chunk.HtmlPath),
                    TextPath = BuildRelativeOptionalPath(path, chunk.TextPath),
                    ManifestPath = BuildRelativeOptionalPath(path, chunk.ManifestPath),
                    chunk.OfflineReadinessGrade,
                    chunk.HighestOfflineRiskSeverity,
                    chunk.OfflineDependencyDiagnosticCount,
                    chunk.OfflineDependencyKindsSummary,
                    chunk.Fingerprint
                }));
                count++;
            }
        }, cancellationToken).ConfigureAwait(false);
        return count;
    }
}
