using System;

namespace HtmlTinkerX;

/// <summary>
/// Represents an asset discovered from a crawled page.
/// </summary>
public sealed class HtmlCrawlAsset {
    /// <summary>Resolved asset URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Page URL that referenced this asset.</summary>
    public string? PageUrl { get; set; }

    /// <summary>Original source attribute value that produced this asset URL.</summary>
    public string? Source { get; set; }

    /// <summary>Asset content type when available.</summary>
    public string? ContentType { get; set; }

    /// <summary>HTTP status code when available.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Local file path when the asset was downloaded.</summary>
    public string? FilePath { get; set; }

    /// <summary>Byte length when available.</summary>
    public long? ContentLength { get; set; }

    /// <summary>Error message when the asset download failed.</summary>
    public string? Error { get; set; }

    /// <summary>Timestamp when download started.</summary>
    public DateTimeOffset Started { get; set; }

    /// <summary>Timestamp when download finished.</summary>
    public DateTimeOffset Finished { get; set; }

    /// <summary>Total download duration.</summary>
    public TimeSpan Duration => Finished - Started;
}
