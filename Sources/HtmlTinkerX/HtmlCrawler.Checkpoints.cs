using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    // In-progress checkpoints store each completed record once. Final/public exports keep
    // their existing self-contained format and rewrite offline links against the complete map.
    private sealed class CrawlCheckpoint {
        public int CheckpointVersion { get; set; } = 1;
        public string Generation { get; set; } = string.Empty;
        public HtmlCrawlResult Result { get; set; } = new();
        public int StoredPageCount { get; set; }
        public int StoredSkippedCount { get; set; }
        public int StoredAssetCount { get; set; }
    }

    private sealed class CrawlCheckpointWriter(string path) {
        private readonly CrawlArtifactPaths _paths = ResolveArtifactPaths(path);
        private readonly string _generation = Guid.NewGuid().ToString("N");
        private int _pages;
        private int _skipped;
        private int _assets;
        private int _publishedPages;
        private int _publishedSkipped;
        private int _publishedAssets;
        private CrawlCheckpoint? _previous;
        private bool _previousInspected;

        public async Task SaveAsync(HtmlCrawlResult result, Queue<CrawlRequest> pending, CancellationToken token) {
            if (!_previousInspected) {
                _previousInspected = true;
                if (File.Exists(_paths.ManifestPath)) {
                    try {
                        string previousJson = await ReadCheckpointTextAsync(_paths.ManifestPath, token).ConfigureAwait(false);
                        _previous = ParseCheckpoint(previousJson);
                    } catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException
                        || ex is IOException || ex is UnauthorizedAccessException) {
                        // Prior metadata is used only for cleanup. Resume validation happens before this writer.
                        _previous = null;
                    }
                }
            }
            result.PendingPages = SnapshotPendingPages(pending);
            SetArtifactPaths(result, _paths);
            string directory = CheckpointDirectory(_paths.ManifestPath, _generation);
            while (_pages < result.Pages.Count) {
                HtmlCrawlPage page = result.Pages[_pages];
                SetPageArtifactPaths(page, _pages, _paths);
                if (!string.IsNullOrEmpty(page.Html)) await WriteTextAsync(page.HtmlPath!, page.Html, token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(page.Text)) await WriteTextAsync(page.TextPath!, page.Text, token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(page.Markdown)) await WriteTextAsync(page.MarkdownPath!, page.Markdown, token).ConfigureAwait(false);
                await WriteRecordAsync(directory, "page", _pages, page, token).ConfigureAwait(false);
                _pages++;
            }
            while (_skipped < result.SkippedPages.Count) {
                await WriteRecordAsync(directory, "skipped", _skipped, result.SkippedPages[_skipped], token).ConfigureAwait(false);
                _skipped++;
            }
            while (_assets < result.Assets.Count) {
                await WriteRecordAsync(directory, "asset", _assets, result.Assets[_assets], token).ConfigureAwait(false);
                _assets++;
            }
            HtmlCrawlResult header = new() {
                StartUrl = result.StartUrl, Started = result.Started, Finished = result.Finished,
                AppliedProfileName = result.AppliedProfileName, AppliedScenario = result.AppliedScenario,
                AppliedProfileReasonCode = result.AppliedProfileReasonCode, AppliedProfileReason = result.AppliedProfileReason,
                RenderEnabled = result.RenderEnabled, AutoRenderEnabled = result.AutoRenderEnabled,
                HiddenContentMode = result.HiddenContentMode, MarkdownProfile = result.MarkdownProfile,
                MarkdownImageMode = result.MarkdownImageMode, ListingCardMetadataMode = result.ListingCardMetadataMode,
                PendingPages = result.PendingPages, SitemapUrls = result.SitemapUrls
            };
            SetArtifactPaths(header, _paths);
            CrawlCheckpoint checkpoint = new() {
                Generation = _generation, Result = header,
                StoredPageCount = _pages, StoredSkippedCount = _skipped, StoredAssetCount = _assets
            };
            await WriteTextAsync(_paths.ManifestPath, JsonSerializer.Serialize(checkpoint, CreateSnapshotJsonOptions()), token).ConfigureAwait(false);
            _publishedPages = _pages;
            _publishedSkipped = _skipped;
            _publishedAssets = _assets;
            if (_previous != null) {
                RemoveCheckpointFiles(_paths.ManifestPath, _previous);
                _previous = null;
            }
        }

        public void RemoveAfterFinalExport() => RemoveCheckpointFiles(_paths.ManifestPath, new CrawlCheckpoint {
            Generation = _generation, StoredPageCount = _pages, StoredSkippedCount = _skipped, StoredAssetCount = _assets
        });

        public void RemoveUnpublished() => RemoveCheckpointFiles(_paths.ManifestPath, new CrawlCheckpoint {
            Generation = _generation, StoredPageCount = _pages, StoredSkippedCount = _skipped, StoredAssetCount = _assets
        }, _publishedPages, _publishedSkipped, _publishedAssets);
    }

    private static CrawlCheckpoint? ParseCheckpoint(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(nameof(CrawlCheckpoint.CheckpointVersion), out JsonElement version)) return null;
        if (version.GetInt32() != 1) throw new InvalidOperationException("Unsupported crawl checkpoint version.");
        CrawlCheckpoint checkpoint = JsonSerializer.Deserialize<CrawlCheckpoint>(json, CreateSnapshotJsonOptions())!;
        if (!Guid.TryParseExact(checkpoint.Generation, "N", out _) || checkpoint.Result == null
            || checkpoint.StoredPageCount < 0 || checkpoint.StoredSkippedCount < 0 || checkpoint.StoredAssetCount < 0) {
            throw new InvalidOperationException("Invalid crawl checkpoint metadata.");
        }
        return checkpoint;
    }

    private static async Task<HtmlCrawlResult> LoadCheckpointAsync(CrawlCheckpoint checkpoint, string manifestPath, CancellationToken token) {
        string directory = CheckpointDirectory(manifestPath, checkpoint.Generation);
        HtmlCrawlResult result = checkpoint.Result;
        for (int index = 0; index < checkpoint.StoredPageCount; index++) result.Pages.Add(await ReadRecordAsync<HtmlCrawlPage>(directory, "page", index, token).ConfigureAwait(false));
        for (int index = 0; index < checkpoint.StoredSkippedCount; index++) result.SkippedPages.Add(await ReadRecordAsync<HtmlCrawlPage>(directory, "skipped", index, token).ConfigureAwait(false));
        for (int index = 0; index < checkpoint.StoredAssetCount; index++) result.Assets.Add(await ReadRecordAsync<HtmlCrawlAsset>(directory, "asset", index, token).ConfigureAwait(false));
        UpdateDerivedResultData(result);
        return result;
    }

    private static string CheckpointDirectory(string manifestPath, string generation) =>
        Path.Combine(manifestPath + ".state", generation);
    private static string RecordPath(string directory, string kind, int index) => Path.Combine(directory, $"{kind}-{index:D8}.json");
    private static Task WriteRecordAsync<T>(string directory, string kind, int index, T value, CancellationToken token) =>
        WriteTextAsync(RecordPath(directory, kind, index), JsonSerializer.Serialize(value, CreateSnapshotJsonOptions()), token);
    private static async Task<T> ReadRecordAsync<T>(string directory, string kind, int index, CancellationToken token) =>
        JsonSerializer.Deserialize<T>(await ReadCheckpointTextAsync(RecordPath(directory, kind, index), token).ConfigureAwait(false), CreateSnapshotJsonOptions())
        ?? throw new InvalidOperationException("Invalid crawl checkpoint record.");
    private static async Task<string> ReadCheckpointTextAsync(string path, CancellationToken token) {
#if NETFRAMEWORK || NETSTANDARD2_0
        return await Task.Run(() => File.ReadAllText(path), token).ConfigureAwait(false);
#else
        return await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
#endif
    }

    private static void RemoveCheckpointFiles(string manifestPath, CrawlCheckpoint checkpoint, int firstPage = 0, int firstSkipped = 0, int firstAsset = 0) {
        string directory = CheckpointDirectory(manifestPath, checkpoint.Generation);
        try {
            for (int index = firstPage; index < checkpoint.StoredPageCount; index++) File.Delete(RecordPath(directory, "page", index));
            for (int index = firstSkipped; index < checkpoint.StoredSkippedCount; index++) File.Delete(RecordPath(directory, "skipped", index));
            for (int index = firstAsset; index < checkpoint.StoredAssetCount; index++) File.Delete(RecordPath(directory, "asset", index));
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory, recursive: false);
            string parent = manifestPath + ".state";
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        } catch (IOException) {
            // A complete manifest is already published. Never remove unknown files or undo it.
        } catch (UnauthorizedAccessException) {
        }
    }
}
