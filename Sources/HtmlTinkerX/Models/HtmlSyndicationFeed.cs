using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents an RSS or Atom feed: its channel-level details and its normalized items.
/// </summary>
public sealed class HtmlSyndicationFeed {
    /// <summary>Feed or channel title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Absolute URL of the site the feed describes when available: the RSS channel <c>link</c> or the Atom alternate link.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// When the feed last changed: the RSS channel <c>lastBuildDate</c>, else its <c>pubDate</c>, else <c>dc:date</c>;
    /// the Atom feed <c>updated</c> (Atom 0.3 <c>modified</c>). Null when the feed does not say.
    /// </summary>
    public DateTimeOffset? Updated { get; set; }

    /// <summary>URL of the feed document when known.</summary>
    public string? SourceFeedUrl { get; set; }

    /// <summary>Items in feed order.</summary>
    public IReadOnlyList<HtmlSyndicationItem> Items { get; set; } = Array.Empty<HtmlSyndicationItem>();
}
