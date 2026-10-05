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
    public static Task<HtmlCrawlResult> LoadResultAsync(string path, CancellationToken cancellationToken = default) =>
        LoadResultAsync(path, cancellationToken, retainPageContent: true);

    private static async Task<HtmlCrawlResult> LoadResultAsync(string path, CancellationToken cancellationToken, bool retainPageContent) {
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

        CrawlCheckpoint? checkpoint = ParseCheckpoint(json);
        if (checkpoint != null) return await LoadCheckpointAsync(checkpoint, manifestPath, cancellationToken, retainPageContent).ConfigureAwait(false);
        JsonSerializerOptions options = CreateSnapshotJsonOptions();
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
        SetArtifactPaths(result, artifactPaths);
        UpdateDerivedResultData(result);

        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];
            using PageContentLease content = new(page);
            SetPageArtifactPaths(page, i, artifactPaths);
        }

        Dictionary<string, string> localPageMap = BuildLocalPageMap(result.Pages);
        Dictionary<string, string> assetMap = result.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Url) && !string.IsNullOrWhiteSpace(asset.FilePath) && string.IsNullOrWhiteSpace(asset.Error))
            .GroupBy(asset => asset.Url, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().FilePath!, StringComparer.Ordinal);

        List<(HtmlCrawlPage Page, string Path)> storedContents = new();
        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];
            using PageContentLease content = new(page);

            if (content.WasStored || options?.RetainPageContent == false) {
                string contentPath = GetContentSidecarPath(page, artifactPaths.PagesDirectory);
                await WriteJsonAtomicallyAsync(contentPath, GetStoredPageContent(page), CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
                storedContents.Add((page, contentPath));
            }

            if (!string.IsNullOrEmpty(page.Html)) {
                string htmlToWrite = ShouldRewriteStoredHtml(options)
                    ? RewriteStoredHtmlToLocalPaths(page.Html, page.ResolutionBaseUrl ?? page.Url, page.HtmlPath!, result.Assets, localPageMap, options!, page.ResolutionBaseUrl != null)
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
                await WriteJsonAtomicallyAsync(page.StructuredJsonPath!, page.StructuredJson, CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
            }

            await WriteTextAsync(page.ManifestPath!, BuildPageManifestJson(page, result.Assets, localPageMap, assetMap), cancellationToken).ConfigureAwait(false);
        }

        await RewriteDownloadedCssAssetsAsync(result.Assets, options, cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < result.SkippedPages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.SkippedPages[i];
            using PageContentLease content = new(page);
            if (content.WasStored || (options?.RetainPageContent == false && HasPageContent(page))) {
                string contentPath = GetContentSidecarPath(page, artifactPaths.PagesDirectory);
                await WriteJsonAtomicallyAsync(contentPath, GetStoredPageContent(page), CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
                storedContents.Add((page, contentPath));
            }
        }

        await ExportPageRecordsAsync(result, artifactPaths, cancellationToken).ConfigureAwait(false);
        await ExportSkippedPageRecordsAsync(result.SkippedPages.Where(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath), artifactPaths.SkippedPagesJsonlPath, cancellationToken).ConfigureAwait(false);
        await ExportSkippedPageRecordsAsync(result.SkippedPages.Where(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath), artifactPaths.SkippedAssetsJsonlPath, cancellationToken).ConfigureAwait(false);
        await ExportLinkRecordsAsync(result, artifactPaths.LinksJsonlPath, cancellationToken).ConfigureAwait(false);
        await ExportAssetRecordsAsync(result, artifactPaths.AssetsJsonlPath, cancellationToken).ConfigureAwait(false);
        await ExportStructuredPageRecordsAsync(result, artifactPaths.StructuredJsonPagesJsonlPath, cancellationToken).ConfigureAwait(false);
        result.ChunkCount = await ExportChunkRecordsAsync(EnumerateChunkRecords(result.Pages), artifactPaths.ChunksJsonlPath, cancellationToken).ConfigureAwait(false);

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
        await WriteJsonAtomicallyAsync(artifactPaths.OpenApiLikeJsonPath, result.OpenApiLike, CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicallyAsync(artifactPaths.OpenApiJsonPath, result.OpenApiDocument, CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicallyAsync(artifactPaths.GraphJsonPath, graphDocument, CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicallyAsync(artifactPaths.SummaryJsonPath, summary, CreateJsonOptions(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SummaryTextPath, summary.ToReportText(result.SitemapUrls), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.IndexHtmlPath, BuildIndexHtml(result, summary, artifactPaths.IndexHtmlPath), cancellationToken).ConfigureAwait(false);

        await WriteJsonAtomicallyAsync(artifactPaths.ManifestPath, result, CreateSnapshotJsonOptions(), cancellationToken).ConfigureAwait(false);
        foreach ((HtmlCrawlPage page, string contentPath) in storedContents) ReleasePageContent(page, contentPath);
    }

    private static void SetArtifactPaths(HtmlCrawlResult result, CrawlArtifactPaths artifactPaths) {
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
    }

    private static void SetPageArtifactPaths(HtmlCrawlPage page, int index, CrawlArtifactPaths artifactPaths) {
        string prefix = (index + 1).ToString("D4");
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
