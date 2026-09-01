using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Observes a rendered crawl page while its prepared browser session is still available.
/// </summary>
/// <remarks>
/// Implementations can export screenshots, evidence bundles, or other browser-backed artifacts
/// without navigating the page a second time. Observers should keep their work bounded and should
/// not navigate the supplied session away from the observed page. Observer failures and cancellation
/// propagate to the caller instead of being converted into page-fetch failures.
/// </remarks>
public interface IHtmlCrawlRenderedPageObserver {
    /// <summary>
    /// Observes a successfully prepared rendered page before the crawler reuses or closes its browser session.
    /// </summary>
    /// <param name="context">Finalized page and page-scoped browser context.</param>
    /// <param name="cancellationToken">Cancellation token for the crawl operation.</param>
    Task ObserveAsync(
        HtmlCrawlRenderedPageContext context,
        CancellationToken cancellationToken = default);
}
