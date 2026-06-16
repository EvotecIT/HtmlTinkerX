using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of extracting a browserless data source.
/// </summary>
public sealed class HtmlBrowserlessExtractionResult {
    /// <summary>Extraction mode. Browserless results always report Browserless.</summary>
    public string Mode { get; set; } = "Browserless";

    /// <summary>Source used for extraction.</summary>
    public HtmlBrowserlessDataSource? Source { get; set; }

    /// <summary>Whether extraction produced a useful result.</summary>
    public bool Success { get; set; }

    /// <summary>Normalized extracted items.</summary>
    public IReadOnlyList<HtmlBrowserlessExtractionItem> Items { get; set; } = Array.Empty<HtmlBrowserlessExtractionItem>();

    /// <summary>HTTP requests performed by extraction.</summary>
    public IReadOnlyList<HtmlBrowserlessExtractionRequest> Requests { get; set; } = Array.Empty<HtmlBrowserlessExtractionRequest>();

    /// <summary>Raw response or payload content when requested.</summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>Detected or reported content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Evidence explaining how the result was produced.</summary>
    public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();

    /// <summary>Warnings that should be reviewed before automating the extraction.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
