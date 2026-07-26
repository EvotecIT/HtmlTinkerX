using System;

namespace HtmlTinkerX;

/// <summary>
/// Options for creating an object-first page document from HTML.
/// </summary>
public sealed class HtmlPageReaderOptions {
    /// <summary>Original requested page URL, when known.</summary>
    public Uri? SourceUri { get; set; }

    /// <summary>Final page URL after redirects or rendering, when known.</summary>
    public Uri? FinalUri { get; set; }

    /// <summary>Base URL used to resolve relative links and resources.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Static or RenderedSnapshot, describing the HTML supplied to the reader.</summary>
    public string AnalysisMode { get; set; } = "Static";

    /// <summary>Optional plain-text hint used to focus repeated-collection discovery.</summary>
    public string? CollectionHint { get; set; }

    /// <summary>Minimum number of repeated elements required for an inferred collection.</summary>
    public int MinimumRepeatCount { get; set; } = 2;

    /// <summary>Maximum number of distinct inferred collections returned.</summary>
    public int CollectionLimit { get; set; } = 5;

    /// <summary>Whether repeated collections should be inferred.</summary>
    public bool IncludeCollections { get; set; } = true;

    /// <summary>Optional OfficeIMO.Html parsing and trust-policy options.</summary>
    public OfficeIMO.Html.HtmlConversionDocumentOptions? ConversionOptions { get; set; }
}
