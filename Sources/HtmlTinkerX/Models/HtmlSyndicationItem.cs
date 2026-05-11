using System;

namespace HtmlTinkerX;

/// <summary>
/// Represents a normalized item from an RSS or Atom feed.
/// </summary>
public sealed class HtmlSyndicationItem {
    /// <summary>Item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Absolute item URL when available.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Item summary or description when available.</summary>
    public string? Summary { get; set; }

    /// <summary>Publication timestamp when available.</summary>
    public DateTimeOffset? Published { get; set; }

    /// <summary>Last update timestamp when available.</summary>
    public DateTimeOffset? Updated { get; set; }

    /// <summary>URL of the feed that produced this item when known.</summary>
    public string? SourceFeedUrl { get; set; }
}
