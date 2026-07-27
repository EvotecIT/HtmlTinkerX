using System;
using System.Collections.Generic;
using OfficeIMO.Html;

namespace HtmlTinkerX;

/// <summary>
/// Object-first view of an HTML page backed by the canonical OfficeIMO.Html semantic document.
/// </summary>
public sealed class HtmlPageDocument {
    /// <summary>Original requested URL, when known.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Final URL after redirects or rendering, when known.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Effective base URL after applying the document base element, when known.</summary>
    public string EffectiveBaseUrl { get; set; } = string.Empty;

    /// <summary>Static or RenderedSnapshot, describing the analyzed HTML.</summary>
    public string AnalysisMode { get; set; } = "Static";

    /// <summary>Best semantic document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Declared document language.</summary>
    public string Language => SemanticDocument.Language;

    /// <summary>Original HTML supplied to the reader.</summary>
    public string Html => Content.SourceHtml;

    /// <summary>Readable text projection.</summary>
    public HtmlReadableTextResult ReadableText { get; set; } = new();

    /// <summary>Markdown projection for display, search, or language-model input.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Canonical OfficeIMO.Html conversion document.</summary>
    public HtmlConversionDocument Content { get; set; } = null!;

    /// <summary>Canonical OfficeIMO.Html logical document.</summary>
    public HtmlLogicalDocument LogicalDocument => Content.LogicalDocument;

    /// <summary>Canonical OfficeIMO.Html typed semantic document.</summary>
    public HtmlSemanticDocument SemanticDocument => Content.SemanticDocument;

    /// <summary>Semantic sections in document order.</summary>
    public IReadOnlyList<HtmlSemanticSection> Sections => SemanticDocument.Sections;

    /// <summary>All retained semantic blocks, including nested list items, in document order.</summary>
    public IReadOnlyList<HtmlSemanticBlock> Blocks { get; set; } = Array.Empty<HtmlSemanticBlock>();

    /// <summary>Heading blocks with text, level, runs, style, and source provenance.</summary>
    public IReadOnlyList<HtmlSemanticBlock> Headings { get; set; } = Array.Empty<HtmlSemanticBlock>();

    /// <summary>Paragraph blocks with text, runs, style, and source provenance.</summary>
    public IReadOnlyList<HtmlSemanticBlock> Paragraphs { get; set; } = Array.Empty<HtmlSemanticBlock>();

    /// <summary>Ordered and unordered list blocks.</summary>
    public IReadOnlyList<HtmlSemanticBlock> Lists { get; set; } = Array.Empty<HtmlSemanticBlock>();

    /// <summary>Typed semantic tables with rows and cells.</summary>
    public IReadOnlyList<HtmlSemanticTable> Tables { get; set; } = Array.Empty<HtmlSemanticTable>();

    /// <summary>Image and media resources retained by OfficeIMO.Html.</summary>
    public IReadOnlyList<HtmlSemanticResource> Resources => SemanticDocument.Resources;

    /// <summary>Normalized page links with resolved URLs.</summary>
    public IReadOnlyList<HtmlPageLink> Links { get; set; } = Array.Empty<HtmlPageLink>();

    /// <summary>Normalized forms discovered on the page.</summary>
    public IReadOnlyList<HtmlDataItem> Forms { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Normalized assets discovered on the page.</summary>
    public IReadOnlyList<HtmlDataItem> Assets { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Repeated record sets inferred without caller-provided selectors.</summary>
    public IReadOnlyList<HtmlPageCollection> Collections { get; set; } = Array.Empty<HtmlPageCollection>();

    /// <summary>Diagnostics emitted by the canonical OfficeIMO.Html parser.</summary>
    public IReadOnlyList<HtmlDiagnostic> Diagnostics => Content.Diagnostics;
}

/// <summary>
/// One normalized page hyperlink.
/// </summary>
public sealed class HtmlPageLink {
    /// <summary>Zero-based link index in document order.</summary>
    public int Index { get; set; }

    /// <summary>Human-readable link text or accessible name.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Resolved absolute URL when a base URL is available.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Original href value from the page.</summary>
    public string RawUrl { get; set; } = string.Empty;

    /// <summary>CSS-like source provenance.</summary>
    public string Selector { get; set; } = string.Empty;
}
