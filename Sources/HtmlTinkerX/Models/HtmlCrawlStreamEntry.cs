namespace HtmlTinkerX;

/// <summary>One entry produced by <see cref="HtmlCrawler.StreamAsync"/> while a crawl runs.</summary>
public sealed class HtmlCrawlStreamEntry {
    private HtmlCrawlStreamEntry(HtmlCrawlStreamEntryKind kind, HtmlCrawlPage? page, object? item, HtmlCrawlResult? result) {
        Kind = kind;
        Page = page;
        Item = item;
        Result = result;
    }

    /// <summary>What this entry carries.</summary>
    public HtmlCrawlStreamEntryKind Kind { get; }

    /// <summary>The page added to the result, or the page whose handler emitted <see cref="Item"/>. Null for the completion entry.</summary>
    public HtmlCrawlPage? Page { get; }

    /// <summary>The emitted item for <see cref="HtmlCrawlStreamEntryKind.Item"/> entries.</summary>
    public object? Item { get; }

    /// <summary>The final crawl result for the <see cref="HtmlCrawlStreamEntryKind.Completed"/> entry.</summary>
    public HtmlCrawlResult? Result { get; }

    internal static HtmlCrawlStreamEntry ForPage(HtmlCrawlPage page) => new(HtmlCrawlStreamEntryKind.Page, page, null, null);

    internal static HtmlCrawlStreamEntry ForItem(HtmlCrawlPage page, object item) => new(HtmlCrawlStreamEntryKind.Item, page, item, null);

    internal static HtmlCrawlStreamEntry ForResult(HtmlCrawlResult result) => new(HtmlCrawlStreamEntryKind.Completed, null, null, result);
}
