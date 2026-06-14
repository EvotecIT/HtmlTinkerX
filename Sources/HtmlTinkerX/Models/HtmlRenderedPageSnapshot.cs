using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a rendered browser page together with common structured extraction results.
/// </summary>
public sealed class HtmlRenderedPageSnapshot {
    /// <summary>Requested page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Final browser URL after redirects and client-side navigation.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Rendered page title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Selector used to produce <see cref="Content"/>, when any.</summary>
    public string? Selector { get; set; }

    /// <summary>Kind of content returned in <see cref="Content"/>.</summary>
    public string ContentKind { get; set; } = "DocumentHtml";

    /// <summary>Focused rendered content selected by the caller, or the full rendered document when no selector was provided.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Full rendered page HTML.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>Full rendered page text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Readable article-like text extracted from the rendered page.</summary>
    public HtmlReadableTextResult? ReadableText { get; set; }

    /// <summary>Markdown converted from the full rendered page HTML.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Common framework application state payloads discovered in rendered HTML.</summary>
    public IReadOnlyList<HtmlAppStateEntry> AppState { get; set; } = Array.Empty<HtmlAppStateEntry>();

    /// <summary>Generic JSON data payloads discovered in script tags.</summary>
    public IReadOnlyList<HtmlScriptDataItem> ScriptData { get; set; } = Array.Empty<HtmlScriptDataItem>();

    /// <summary>Script elements discovered in rendered HTML.</summary>
    public IReadOnlyList<HtmlScriptReference> Scripts { get; set; } = Array.Empty<HtmlScriptReference>();

    /// <summary>Likely endpoint strings discovered in inline rendered JavaScript.</summary>
    public IReadOnlyList<HtmlJavaScriptEndpoint> JavaScriptEndpoints { get; set; } = Array.Empty<HtmlJavaScriptEndpoint>();

    /// <summary>Likely endpoint strings discovered in linked JavaScript files when explicitly requested.</summary>
    public IReadOnlyList<HtmlLinkedJavaScriptEndpoint> LinkedJavaScriptEndpoints { get; set; } = Array.Empty<HtmlLinkedJavaScriptEndpoint>();

    /// <summary>Likely token values discovered in forms, metadata, attributes, or inline scripts.</summary>
    public IReadOnlyList<HtmlToken> Tokens { get; set; } = Array.Empty<HtmlToken>();

    /// <summary>Normalized structured data, links, assets, forms, tokens, and app state discovered in rendered HTML.</summary>
    public IReadOnlyList<HtmlDataItem> Data { get; set; } = Array.Empty<HtmlDataItem>();

    /// <summary>Common JavaScript configuration and framework state values discovered in rendered HTML.</summary>
    public IReadOnlyList<HtmlJavaScriptConfigItem> JavaScriptConfig { get; set; } = Array.Empty<HtmlJavaScriptConfigItem>();

    /// <summary>Forms, hidden fields, tokens, and inline endpoints discovered in rendered HTML.</summary>
    public IReadOnlyList<HtmlInteractionSurfaceItem> InteractionSurface { get; set; } = Array.Empty<HtmlInteractionSurfaceItem>();

    /// <summary>Static-vs-rendered data comparison when explicitly requested.</summary>
    public HtmlStaticRenderedComparison? StaticRenderedComparison { get; set; }

    /// <summary>Rendered-page interactions successfully applied before snapshot extraction.</summary>
    public IReadOnlyList<string> AppliedInteractions { get; set; } = Array.Empty<string>();

    /// <summary>Browser console entries captured during rendering.</summary>
    public IReadOnlyList<HtmlConsoleEntry> ConsoleLog { get; set; } = Array.Empty<HtmlConsoleEntry>();

    /// <summary>Browser network entries captured during rendering when explicitly requested.</summary>
    public IReadOnlyList<HtmlNetworkEntry> NetworkLog { get; set; } = Array.Empty<HtmlNetworkEntry>();
}
