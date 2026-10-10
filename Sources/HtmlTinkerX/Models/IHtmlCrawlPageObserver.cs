using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>Processes each newly fetched crawl page before its checkpoint is committed.</summary>
/// <remarks>
/// Receives successful and failed pages included in the result, in crawl order. Skipped candidates and
/// pages loaded from a previous checkpoint are not replayed. The crawler awaits each callback; failures
/// and cancellation propagate to its caller. An interrupted callback may run again when the crawl resumes,
/// so external writes should tolerate retries. Export paths are assigned by the subsequent checkpoint or export.
/// Pages remain in the final result. Observers should not modify them.
/// When content retention is disabled, the crawler clears a page's content after the callback and checkpoint.
/// Call <see cref="HtmlCrawlPage.CreateSnapshot"/> inside the callback to keep its current content afterward.
/// </remarks>
public interface IHtmlCrawlPageObserver {
    /// <summary>Processes a fetched page after extraction and optional asset downloads.</summary>
    /// <param name="page">Page added to the crawl result.</param>
    /// <param name="cancellationToken">Cancellation token for the crawl.</param>
    Task ObserveAsync(HtmlCrawlPage page, CancellationToken cancellationToken = default);
}
