namespace HtmlTinkerX;

/// <summary>Kind of entry produced by a streamed crawl.</summary>
public enum HtmlCrawlStreamEntryKind {
    /// <summary>A newly fetched page was added to the result, including fetch failures.</summary>
    Page,
    /// <summary>A page handler emitted an item.</summary>
    Item,
    /// <summary>The crawl finished; this is the last entry and carries the result.</summary>
    Completed
}
