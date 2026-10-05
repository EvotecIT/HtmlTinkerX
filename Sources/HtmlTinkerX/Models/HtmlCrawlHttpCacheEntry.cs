using System.Collections.Generic;

namespace HtmlTinkerX;

// Persisted only when explicitly enabled or while refreshing. The original HTTP body
// allows current extraction settings to be applied after successful revalidation.
internal sealed class HtmlCrawlHttpCacheEntry {
    public HtmlCrawlHttpCacheEntry() { }

    public string Html { get; set; } = string.Empty;
    public int ByteLength { get; set; }
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
}
