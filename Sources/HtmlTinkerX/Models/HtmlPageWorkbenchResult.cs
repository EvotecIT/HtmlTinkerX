using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// One-page intelligence result composed from the reusable HtmlTinkerX parsing surfaces.
/// </summary>
public sealed class HtmlPageWorkbenchResult {
    /// <summary>Source URL or base URL used during analysis, when known.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Final URL after rendering or redirects, when known.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Content view used for the primary grouped extraction results.</summary>
    public string AnalysisMode { get; set; } = "Static";

    /// <summary>Best page title discovered during static analysis.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Original HTML when requested.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>Readable article-like text extracted from the page.</summary>
    public HtmlReadableTextResult? ReadableText { get; set; }

    /// <summary>Markdown converted from the page HTML.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Recommended next extraction workflow.</summary>
    public HtmlExtractionPlan? ExtractionPlan { get; set; }

    /// <summary>Rendered browser snapshot used to enrich this result, when supplied.</summary>
    public HtmlRenderedPageSnapshot? RenderedSnapshot { get; set; }

    /// <summary>Static-vs-rendered comparison when a rendered snapshot is available.</summary>
    public HtmlStaticRenderedComparison? StaticRenderedComparison { get; set; }

    /// <summary>PowerShell command that can be used as the next step.</summary>
    public string SuggestedNextCommand { get; set; } = string.Empty;

    /// <summary>Warnings about auth, sensitivity, rendering, or extraction risk.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    /// <summary>All normalized structured data, links, assets, forms, tokens, and app-state records.</summary>
    public IReadOnlyList<HtmlDataItem> Data { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Normalized data records from the original static HTML.</summary>
    public IReadOnlyList<HtmlDataItem> StaticData { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Normalized data records from the rendered snapshot, when supplied.</summary>
    public IReadOnlyList<HtmlDataItem> RenderedData { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Form records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> Forms { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Link records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> Links { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Asset records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> Assets { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>JSON-LD records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> JsonLd { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>OpenGraph records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> OpenGraph { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Application-state records discovered in <see cref="Data"/>.</summary>
    public IReadOnlyList<HtmlDataItem> AppState { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Common JavaScript configuration and framework state values discovered in inline scripts.</summary>
    public IReadOnlyList<HtmlJavaScriptConfigItem> JavaScriptConfig { get; set; } = Array.Empty<HtmlJavaScriptConfigItem>();

    /// <summary>Forms, hidden fields, tokens, and endpoint surfaces discovered in the page.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> InteractionSurface { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Interaction surfaces discovered in the original static HTML.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> StaticInteractionSurface { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Interaction surfaces discovered in the rendered snapshot, when supplied.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> RenderedInteractionSurface { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Hidden form fields discovered in <see cref="InteractionSurface"/>.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> HiddenFields { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Token surfaces discovered in <see cref="InteractionSurface"/>.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> Tokens { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Inline or linked JavaScript endpoints discovered in <see cref="InteractionSurface"/>.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> Endpoints { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Classified API and form endpoint inventory.</summary>
    public IReadOnlyList<HtmlApiEndpointRecord> ApiEndpoints { get; set; } = Array.Empty<HtmlApiEndpointRecord>();

    /// <summary>Number of normalized structured records.</summary>
    public int DataItemCount { get; set; }

    /// <summary>Number of forms discovered.</summary>
    public int FormCount { get; set; }

    /// <summary>Number of hidden fields discovered.</summary>
    public int HiddenFieldCount { get; set; }

    /// <summary>Number of links discovered.</summary>
    public int LinkCount { get; set; }

    /// <summary>Number of assets discovered.</summary>
    public int AssetCount { get; set; }

    /// <summary>Number of endpoint surfaces discovered.</summary>
    public int EndpointCount { get; set; }

    /// <summary>Number of classified API and form endpoints discovered.</summary>
    public int ApiEndpointCount { get; set; }

    /// <summary>Number of JavaScript configuration records discovered.</summary>
    public int JavaScriptConfigCount { get; set; }
}
