using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    /// <summary>
    /// Crawls a site and streams pages and handler items while the crawl runs.
    /// </summary>
    /// <param name="startUrl">URL to begin crawling from.</param>
    /// <param name="options">Crawler options.</param>
    /// <param name="pageHandler">Optional handler awaited for each successful page before its links are queued and its checkpoint is committed.
    /// It can emit items and queue follow-up requests through <see cref="HtmlCrawlPageContext"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="HtmlCrawlStreamEntryKind.Page"/> entry for each newly fetched result page, followed by its
    /// <see cref="HtmlCrawlStreamEntryKind.Item"/> entries, and a final <see cref="HtmlCrawlStreamEntryKind.Completed"/> entry with the result.
    /// </returns>
    /// <remarks>
    /// The crawl waits while the consumer processes an entry, so a slow consumer slows the crawl instead of buffering it.
    /// Handler failures and crawl failures surface from the enumeration, as they do from <see cref="CrawlAsync"/>.
    /// Ending the enumeration early cancels the crawl. Items are not stored in the result or in checkpoints; a resumed
    /// crawl does not replay handlers for pages it already committed. Pages are streamed as snapshots when
    /// <see cref="HtmlCrawlOptions.RetainPageContent"/> is false.
    /// </remarks>
    public static async IAsyncEnumerable<HtmlCrawlStreamEntry> StreamAsync(
        string startUrl,
        HtmlCrawlOptions? options = null,
        Func<HtmlCrawlPageContext, CancellationToken, Task>? pageHandler = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        HtmlCrawlOptions streamOptions = options?.Clone() ?? new HtmlCrawlOptions();
        Channel<HtmlCrawlStreamEntry> channel = Channel.CreateBounded<HtmlCrawlStreamEntry>(
            new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true });
        streamOptions.PageHandler = pageHandler;
        streamOptions.StreamWriter = channel.Writer;
        using CancellationTokenSource crawlCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<HtmlCrawlResult> crawl = RunAsync();
        async Task<HtmlCrawlResult> RunAsync() {
            try {
                return await CrawlAsync(startUrl, streamOptions, crawlCancellation.Token).ConfigureAwait(false);
            } finally {
                channel.Writer.TryComplete();
            }
        }

        try {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                while (channel.Reader.TryRead(out HtmlCrawlStreamEntry? entry)) {
                    yield return entry;
                }
            }
            yield return HtmlCrawlStreamEntry.ForResult(await crawl.ConfigureAwait(false));
        } finally {
            crawlCancellation.Cancel();
            try {
                await crawl.ConfigureAwait(false);
            } catch {
                // A crawl failure was already rethrown by the final await, or the consumer stopped early and cancelled the crawl.
            }
        }
    }
}
