using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Self-contained document-style page data intended for downstream JSON consumers.
/// </summary>
public sealed class HtmlCrawlStructuredDocument {
    /// <summary>Resolved page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Original URL requested before any canonical rewrite.</summary>
    public string? RequestedUrl { get; set; }

    /// <summary>URL that discovered this page.</summary>
    public string? ParentUrl { get; set; }

    /// <summary>Canonical URL discovered in the page markup, when available.</summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>Distance from the starting URL.</summary>
    public int Depth { get; set; }

    /// <summary>Page title when available.</summary>
    public string? Title { get; set; }

    /// <summary>Response content type when available.</summary>
    public string? ContentType { get; set; }

    /// <summary>Indicates whether the page was rendered in a browser.</summary>
    public bool Rendered { get; set; }

    /// <summary>Describes whether the page stayed static, was rendered directly, or was auto-rendered after a static pass.</summary>
    public HtmlCrawlRenderMode RenderMode { get; set; }

    /// <summary>Categorizes why the selected render mode was used for this page.</summary>
    public HtmlCrawlRenderReasonCode RenderReasonCode { get; set; }

    /// <summary>Records which content-selection mode produced the stored content for this page.</summary>
    public HtmlCrawlContentMode ContentModeUsed { get; set; }

    /// <summary>Categorizes how the crawler chose the content region used for extraction.</summary>
    public HtmlCrawlContentSelectionReasonCode ContentSelectionReasonCode { get; set; }

    /// <summary>Explains how the crawler chose the content region used for extraction.</summary>
    public string? ContentSelectionReason { get; set; }

    /// <summary>Compact selector-like hint for the selected content element when available.</summary>
    public string? ContentElementSelectorHint { get; set; }

    /// <summary>Number of words in the extracted document text.</summary>
    public int WordCount { get; set; }

    /// <summary>Number of characters in the extracted document text.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Number of generated chunks for the extracted document text.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Number of discovered outbound links for the page.</summary>
    public int LinkCount { get; set; }

    /// <summary>Number of discovered asset references for the page.</summary>
    public int AssetCount { get; set; }

    /// <summary>Number of applied rendered interactions for the page.</summary>
    public int InteractionCount { get; set; }

    /// <summary>Short summary of the extracted document text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Headings extracted from the selected content.</summary>
    public IList<string> Headings { get; set; } = new List<string>();

    /// <summary>Keywords extracted from the document text.</summary>
    public IList<string> Keywords { get; set; } = new List<string>();

    /// <summary>Normalized outbound links discovered on the page.</summary>
    public IList<string> Links { get; set; } = new List<string>();

    /// <summary>Rendered interactions that were successfully applied before extraction.</summary>
    public IList<string> AppliedInteractions { get; set; } = new List<string>();

    /// <summary>Extracted plain text for the selected content.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Markdown representation of the selected content.</summary>
    public string Markdown { get; set; } = string.Empty;
}
