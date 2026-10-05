using System;

namespace HtmlTinkerX;

/// <summary>A page or child sitemap advertised by a sitemap document.</summary>
public sealed class HtmlSitemapEntry {
    /// <summary>The location resolved against the sitemap URL when available.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The optional last modification date supplied by the publisher.</summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>Whether this entry identifies a child sitemap rather than a page.</summary>
    public bool IsSitemap { get; set; }
}
