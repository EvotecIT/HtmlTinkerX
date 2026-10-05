using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    // The coordinator leaves prefetched candidates in its pending queue until consumption,
    // so every published checkpoint still includes downloads that have not been committed.
    private sealed class CrawlPrefetchWindow(CancellationToken token) {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        private readonly Dictionary<string, Task<FetchedPageData>> _downloads = new(StringComparer.Ordinal);
        internal int Count => _downloads.Count;

        internal void Add(string key, CrawlRequest request, HttpClient client, HtmlCrawlOptions options,
            IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> schema, HtmlCrawlPage? cached) {
            HtmlCrawlOptions downloadOptions = options.Clone();
            downloadOptions.CrawlOrigin = options.CrawlOrigin;
            downloadOptions.PageResponseBudget = options.PageResponseBudget;
            _downloads.Add(key, DownloadAsync());
            async Task<FetchedPageData> DownloadAsync() {
                try {
                    return await FetchHttpPageAsync(client, request, downloadOptions, schema, _stop.Token, cached, deferExtraction: true).ConfigureAwait(false);
                } finally {
                    downloadOptions.ClearSensitiveData();
                }
            }
        }

        internal bool TryTake(string key, out Task<FetchedPageData>? task) {
            if (!_downloads.TryGetValue(key, out task)) return false;
            _downloads.Remove(key);
            return true;
        }

        internal async Task StopAsync() {
            try {
                _stop.Cancel();
                try { await Task.WhenAll(_downloads.Values).ConfigureAwait(false); }
                catch { /* Preserve the coordinator's original failure; observe every abandoned download. */ }
            } finally {
                _stop.Dispose();
            }
        }
    }

    private static void PopulatePrefetchedPage(FetchedPageData fetched, HtmlCrawlOptions options,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> schema) {
        if (fetched.Page.Status != HtmlCrawlPageStatus.Success || fetched.RawHtml == null) return;
        try {
            PopulatePageFromHtml(fetched.Page, fetched.RawHtml, fetched.DocumentUri!, options, schema);
        } catch (Exception error) {
            fetched.Page.Status = HtmlCrawlPageStatus.Failed;
            fetched.Page.Error = error.Message;
            fetched.Page.HttpCache = null;
        } finally {
            fetched.Page.Finished = DateTimeOffset.UtcNow;
        }
    }
}
