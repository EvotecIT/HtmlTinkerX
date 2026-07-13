using Acornima;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Normalized structured item discovered in an HTML document.
/// </summary>
/// <example>
/// <code>
/// var items = HtmlParsingToolbox.SelectData(html, new [] { "JsonLd", "OpenGraph", "Form" }, new Uri("https://example.org/"));
/// foreach (HtmlDataItem item in items) {
///     Console.WriteLine($"{item.Kind}: {item.Name} from {item.Selector}");
/// }
/// </code>
/// </example>
public sealed class HtmlDataItem {
    /// <summary>Source-order index within the normalized result set.</summary>
    public int Index { get; set; }

    /// <summary>Data family, such as JsonLd, Microdata, OpenGraph, Form, Link, Asset, Token, AppState, or ScriptData.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best human-readable name for the item.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional type or framework value for the item.</summary>
    public string? Type { get; set; }

    /// <summary>Optional identifier associated with the source item.</summary>
    public string? Id { get; set; }

    /// <summary>Parsed or compact value intended for programmatic consumers.</summary>
    public object? Value { get; set; }

    /// <summary>Raw string value, JSON payload, or original URL value when available.</summary>
    public string RawValue { get; set; } = string.Empty;

    /// <summary>CSS-like selector hint for the source element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Short source label, such as Script, Meta, Form, Link, or Asset.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Index from the original parser when available, such as script, form, or asset index.</summary>
    public int? SourceIndex { get; set; }
}

/// <summary>
/// JavaScript application configuration value discovered in inline scripts.
/// </summary>
/// <example>
/// <code>
/// var configs = HtmlParsingToolbox.SelectJavaScriptConfig(html, new [] { "window.__CONFIG__" }, propertyPaths: new [] { "api.baseUrl" });
/// string? baseUrl = configs.FirstOrDefault()?.Value?.ToString();
/// </code>
/// </example>
public sealed class HtmlJavaScriptConfigItem {
    /// <summary>Source-order index within the result set.</summary>
    public int Index { get; set; }

    /// <summary>Matched variable name or assignment member name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full variable or assignment path, such as window.__CONFIG__.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Declaration kind or Assignment.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Dotted property path read from the matched object literal when requested.</summary>
    public string? PropertyPath { get; set; }

    /// <summary>Static value evaluated from the JavaScript literal.</summary>
    public object? Value { get; set; }

    /// <summary>Raw literal text or JSON-normalized representation when available.</summary>
    public string RawValue { get; set; } = string.Empty;

    /// <summary>Source script index.</summary>
    public int? ScriptIndex { get; set; }

    /// <summary>Source script type attribute.</summary>
    public string? ScriptType { get; set; }

    /// <summary>CSS-like selector hint for the source script element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Source label used to distinguish JavaScript variables from known framework state scripts.</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// CSS selector usage result for stylesheet rules compared with an HTML document.
/// </summary>
/// <example>
/// <code>
/// var usage = HtmlParsingToolbox.SelectStyleUsage(html);
/// var unusedRules = usage.Where(rule => !rule.IsUsed &amp;&amp; string.IsNullOrEmpty(rule.Error));
/// </code>
/// </example>
public sealed class HtmlStyleUsageItem {
    /// <summary>Source-order index within the result set.</summary>
    public int Index { get; set; }

    /// <summary>Selector text from the CSS style rule.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Whether the selector matched at least one element in the document.</summary>
    public bool IsUsed { get; set; }

    /// <summary>Total matched element count across the selector or selector list.</summary>
    public int MatchCount { get; set; }

    /// <summary>Selector hints for representative matched elements.</summary>
    public string[] MatchedElements { get; set; } = Array.Empty<string>();

    /// <summary>Original CSS rule text.</summary>
    public string CssText { get; set; } = string.Empty;

    /// <summary>Parent rule context, such as @media or @supports, when present.</summary>
    public string? Context { get; set; }

    /// <summary>Source label, such as StyleElement or CssContent.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Index of the style source that produced the rule.</summary>
    public int SourceIndex { get; set; }

    /// <summary>Selector parse or match error, when the selector cannot be evaluated by the HTML selector engine.</summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Form, token, or endpoint surface discovered in an HTML document.
/// </summary>
/// <example>
/// <code>
/// var surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(html, new Uri("https://example.org/"), includeLinkedScripts: true);
/// foreach (HtmlInteractionSurfaceItem surface in surfaces.Where(item => item.Kind == "Endpoint")) {
///     Console.WriteLine($"{surface.Method} {surface.Url}");
/// }
/// </code>
/// </example>
public sealed class HtmlInteractionSurfaceItem {
    /// <summary>Source-order index within the interaction result set.</summary>
    public int Index { get; set; }

    /// <summary>Interaction kind, such as Form, Field, Token, Endpoint, or LinkedEndpoint.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Best name for the form, field, token, or endpoint.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HTTP method when known.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Target URL or endpoint path when known.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Value associated with tokens, hidden fields, or diagnostics.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>CSS-like selector hint for the source element.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Source label, such as Form, Field, Token, InlineScript, or LinkedScript.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Original parser index when available.</summary>
    public int? SourceIndex { get; set; }

    /// <summary>Whether the endpoint came from a cross-origin linked script.</summary>
    public bool IsExternal { get; set; }

    /// <summary>Additional context such as client, operation name, or download error.</summary>
    public string Metadata { get; set; } = string.Empty;
}

/// <summary>
/// Added or removed signatures for one data kind in a static-vs-rendered comparison.
/// </summary>
public sealed class HtmlStaticRenderedDelta {
    /// <summary>Data kind compared, such as Link, Form, JsonLd, AppState, ScriptData, or Token.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Number of matching static signatures.</summary>
    public int StaticCount { get; set; }

    /// <summary>Number of matching rendered signatures.</summary>
    public int RenderedCount { get; set; }

    /// <summary>Signatures present only in rendered HTML.</summary>
    public string[] Added { get; set; } = Array.Empty<string>();

    /// <summary>Signatures present only in static HTML.</summary>
    public string[] Removed { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Summary of differences between original static HTML and browser-rendered HTML.
/// </summary>
/// <example>
/// <code>
/// HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(staticHtml, renderedHtml);
/// foreach (HtmlStaticRenderedDelta delta in comparison.Deltas.Where(item => item.Added.Length &gt; 0)) {
///     Console.WriteLine($"{delta.Kind} added after rendering");
/// }
/// </code>
/// </example>
public sealed class HtmlStaticRenderedComparison {
    /// <summary>Length of the original static HTML.</summary>
    public int StaticHtmlLength { get; set; }

    /// <summary>Length of the rendered HTML.</summary>
    public int RenderedHtmlLength { get; set; }

    /// <summary>Readable text length in the original static HTML.</summary>
    public int StaticTextLength { get; set; }

    /// <summary>Readable text length in the rendered HTML.</summary>
    public int RenderedTextLength { get; set; }

    /// <summary>Number of anchor links in the original static HTML.</summary>
    public int StaticLinkCount { get; set; }

    /// <summary>Number of anchor links in the rendered HTML.</summary>
    public int RenderedLinkCount { get; set; }

    /// <summary>Number of forms in the original static HTML.</summary>
    public int StaticFormCount { get; set; }

    /// <summary>Number of forms in the rendered HTML.</summary>
    public int RenderedFormCount { get; set; }

    /// <summary>Number of JSON-LD items in the original static HTML.</summary>
    public int StaticJsonLdCount { get; set; }

    /// <summary>Number of JSON-LD items in the rendered HTML.</summary>
    public int RenderedJsonLdCount { get; set; }

    /// <summary>Per-kind added and removed signatures.</summary>
    public IReadOnlyList<HtmlStaticRenderedDelta> Deltas { get; set; } = Array.Empty<HtmlStaticRenderedDelta>();
}
