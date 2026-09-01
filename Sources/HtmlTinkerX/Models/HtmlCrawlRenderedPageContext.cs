using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Describes a finalized rendered crawl page while its browser session is still positioned on it.
/// </summary>
/// <remarks>
/// The session remains owned by the crawler and is valid only for the duration of the observer call.
/// <see cref="NetworkLog"/> contains only entries captured while loading and preparing this page.
/// </remarks>
public sealed class HtmlCrawlRenderedPageContext {
    /// <summary>Creates a rendered-page observer context.</summary>
    /// <param name="session">Browser session positioned on the rendered page.</param>
    /// <param name="page">Finalized crawl page.</param>
    /// <param name="networkLog">Network entries scoped to this page.</param>
    public HtmlCrawlRenderedPageContext(
        HtmlBrowserSession session,
        HtmlCrawlPage page,
        IReadOnlyList<HtmlNetworkEntry> networkLog) {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Page = page ?? throw new ArgumentNullException(nameof(page));
        NetworkLog = networkLog ?? throw new ArgumentNullException(nameof(networkLog));
    }

    /// <summary>Browser session positioned on the observed page.</summary>
    public HtmlBrowserSession Session { get; }

    /// <summary>Finalized crawl page, including render reason and run metadata.</summary>
    public HtmlCrawlPage Page { get; }

    /// <summary>Network entries captured while loading and preparing this page.</summary>
    public IReadOnlyList<HtmlNetworkEntry> NetworkLog { get; }
}
