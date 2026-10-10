using System;
using System.IO;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    // Checkpoint page records and final content sidecars contain the same body fields.
    // Only opt-in metadata-only results use these paths; ordinary snapshots stay self-contained.
    private sealed class StoredPageContent {
        public string Html { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Markdown { get; set; } = string.Empty;
        public HtmlCrawlHttpCacheEntry? HttpCache { get; set; }
    }

    private sealed class PageContentLease : IDisposable {
        private readonly HtmlCrawlPage? _page;
        private readonly string? _path;
        private readonly string _html = string.Empty;
        private readonly string _text = string.Empty;
        private readonly string _markdown = string.Empty;
        private readonly HtmlCrawlHttpCacheEntry? _cache;

        public bool WasStored => _path != null;

        public PageContentLease(HtmlCrawlPage page) {
            if (page.StoredContentPath == null) return;
            _path = page.StoredContentPath;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_path));
            foreach (string field in new[] { nameof(page.Html), nameof(page.Text), nameof(page.Markdown) }) {
                if (!document.RootElement.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.String) {
                    throw new JsonException($"Invalid stored crawl content: missing string '{field}'.");
                }
            }
            StoredPageContent content = JsonSerializer.Deserialize<StoredPageContent>(document.RootElement.GetRawText(), CreateJsonOptions())!;
            _page = page;
            _html = page.Html; _text = page.Text; _markdown = page.Markdown; _cache = page.HttpCache;
            // Preserve nonempty content explicitly supplied by the caller. LoadResultAsync
            // provides fully retained pages when callers need to edit or remove body content.
            if (page.Html.Length == 0) page.Html = content.Html;
            if (page.Text.Length == 0) page.Text = content.Text;
            if (page.Markdown.Length == 0) page.Markdown = content.Markdown;
            page.HttpCache ??= content.HttpCache;
            page.StoredContentPath = null; // Nested analysis/serialization uses this lease.
        }

        public void Dispose() {
            if (_page == null) return;
            _page.Html = _html; _page.Text = _text; _page.Markdown = _markdown; _page.HttpCache = _cache;
            _page.StoredContentPath = _path;
        }
    }

    private static void ReleasePageContent(HtmlCrawlPage page, string path) {
        page.StoredContentPath = path;
        page.Html = string.Empty; page.Text = string.Empty; page.Markdown = string.Empty;
        page.HttpCache = null;
    }

    private static bool HasPageContent(HtmlCrawlPage page) => page.StoredContentPath != null
        || page.Html.Length > 0 || page.Text.Length > 0 || page.Markdown.Length > 0 || page.HttpCache != null;

    private static StoredPageContent GetStoredPageContent(HtmlCrawlPage page) => new() {
        Html = page.Html, Text = page.Text, Markdown = page.Markdown, HttpCache = page.HttpCache
    };

    private static string GetContentSidecarPath(HtmlCrawlPage page, string directory) {
        // List order and titles are mutable. Keep each page's content identity stable
        // so resaving cannot overwrite another page's still-referenced source.
        page.StoredContentId ??= Guid.NewGuid().ToString("N");
        return Path.Combine(directory, "content-" + page.StoredContentId + ".content.json");
    }
}
