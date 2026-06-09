using Acornima;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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

/// <summary>
/// Higher-level HTML parsing workflows that compose the lower-level HtmlTinkerX parsers into provenance-friendly results.
/// </summary>
public static class HtmlParsingToolbox {
    private static readonly string[] DefaultConfigNames = {
        "config",
        "settings",
        "state",
        "__CONFIG__",
        "__INITIAL_STATE__",
        "__APP_STATE__",
        "__NEXT_DATA__",
        "__NUXT__",
        "dataLayer"
    };

    /// <summary>
    /// Selects common structured data families from an HTML document and returns normalized provenance-friendly records.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="kinds">Optional data kinds to include. Supported values include JsonLd, Microdata, OpenGraph, Meta, HeadLink, AppState, ScriptData, Token, Form, Link, and Asset.</param>
    /// <param name="baseUri">Optional page URL used to resolve relative links and assets.</param>
    /// <returns>Normalized data records in source-family order.</returns>
    public static IReadOnlyList<HtmlDataItem> SelectData(string html, IReadOnlyCollection<string>? kinds = null, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HashSet<string>? filter = CreateKindFilter(kinds);
        List<HtmlDataItem> items = new();
        Uri? effectiveBaseUri = GetEffectiveBaseUri(html, baseUri);

        if (Includes(filter, "JsonLd")) {
            foreach (HtmlJsonLdItem item in HtmlJsonLdParser.Parse(html)) {
                Add(items, "JsonLd", FirstNonEmpty(item.Type, item.Id, "@jsonld"), item.Type, item.Id, item.RawJson, item.RawJson, $"script:nth-of-type({item.ScriptIndex + 1})", "Script", item.ScriptIndex);
            }
        }

        if (Includes(filter, "AppState")) {
            foreach (HtmlAppStateEntry item in HtmlAppStateParser.Parse(html)) {
                Add(items, "AppState", item.Name, item.Framework, null, item.RawJson, item.RawJson, $"script:nth-of-type({item.ScriptIndex + 1})", item.SourceKind, item.ScriptIndex);
            }
        }

        if (Includes(filter, "ScriptData")) {
            foreach (HtmlScriptDataItem item in HtmlScriptDataParser.Parse(html)) {
                Add(items, "ScriptData", FirstNonEmpty(item.Id, item.Type, "script-data"), item.Type, item.Id, item.RawJson, item.RawJson, item.Selector, item.SourceKind, item.ScriptIndex);
            }
        }

        if (Includes(filter, "HeadLink")) {
            foreach (HtmlHeadLink item in HtmlHeadLinkParser.Parse(html, baseUri, effectiveBaseUri)) {
                Add(items, "HeadLink", FirstNonEmpty(item.Rel, item.Name, item.Property, item.Element), item.Type, null, FirstNonEmpty(item.Url, item.Href, item.Content), FirstNonEmpty(item.Href, item.Content, item.Url), item.Selector, item.Element, item.Index);
            }
        }

        if (Includes(filter, "Meta")) {
            foreach (HtmlMetaTag item in HtmlParser.ParseMetaTags(html)) {
                string attribute = FirstNonEmpty(item.SourceAttribute, "name");
                Add(items, "Meta", item.Name, null, null, item.Content, item.Content, CreateAttributeSelector("meta", attribute, item.Name), "Meta", null);
            }
        }

        if (Includes(filter, "OpenGraph")) {
            foreach (OpenGraphProperty property in HtmlParser.ParseOpenGraph(html).Properties) {
                foreach (string value in property.Values) {
                    Add(items, "OpenGraph", property.Name, null, null, value, value, CreateAttributeSelector("meta", "property", "og:" + property.Name), "Meta", null);
                }
            }
        }

        if (Includes(filter, "Microdata")) {
            foreach (HtmlMicrodataItem item in HtmlParser.ParseMicrodataItems(html)) {
                Add(items, "Microdata", FirstNonEmpty(item.Type, item.Id, "microdata"), item.Type, item.Id, item.Properties, SerializeValue(item.Properties), "[itemscope]", "Microdata", null);
            }
        }

        if (Includes(filter, "Token")) {
            foreach (HtmlToken item in HtmlTokenParser.Parse(html)) {
                Add(items, "Token", item.Name, null, null, item.Value, item.Value, item.Selector, item.Source, item.Index);
            }
        }

        if (Includes(filter, "Form")) {
            foreach (HtmlFormResult form in HtmlParser.ParseFormsWithAngleSharp(html)) {
                string formName = FirstNonEmpty(form.Metadata.Id, $"form[{form.Metadata.FormIndex}]");
                string actionTarget = ResolveUrlValue(form.Metadata.Action, effectiveBaseUri);
                Add(items, "Form", formName, form.Metadata.Method.ToString().ToUpperInvariant(), form.Metadata.Id, form.Fields.Select(field => field.Name).Where(static name => !string.IsNullOrWhiteSpace(name)).ToArray(), actionTarget, CreateFormSelector(form.Metadata), "Form", form.Metadata.FormIndex);
            }
        }

        if (Includes(filter, "Link")) {
            foreach (HtmlDiscoveredLink link in HtmlDiscoveryParser.ParseLinks(html, effectiveBaseUri)) {
                Add(items, "Link", FirstNonEmpty(link.Text, link.Title, link.Url), null, null, link.Url, link.Href, "a[href]", "Anchor", null);
            }
        }

        if (Includes(filter, "Asset")) {
            foreach (HtmlAssetReference asset in HtmlWorkflowParser.SelectAssets(html, baseUri)) {
                Add(items, "Asset", asset.Kind, asset.Type, null, FirstNonEmpty(asset.ResolvedUrl, asset.Url, asset.Content), FirstNonEmpty(asset.Url, asset.Content), asset.Element, asset.Attribute, asset.Index);
            }
        }

        return items;
    }

    /// <summary>
    /// Selects JavaScript application configuration and known framework state from inline scripts.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="names">Optional variable names or assignment paths. When omitted, common config and state names are matched by containment.</param>
    /// <param name="contains">Matches names or paths that contain the provided names.</param>
    /// <param name="startsWith">Matches names or paths that start with the provided names.</param>
    /// <param name="propertyPaths">Optional dotted property paths to extract from matched object literals.</param>
    /// <param name="includeAppState">Includes known framework state payloads such as __NEXT_DATA__ alongside JavaScript variables.</param>
    /// <param name="tolerant">Enables tolerant JavaScript parsing.</param>
    /// <returns>Configuration records in source order.</returns>
    public static IReadOnlyList<HtmlJavaScriptConfigItem> SelectJavaScriptConfig(
        string html,
        IReadOnlyList<string>? names = null,
        bool contains = false,
        bool startsWith = false,
        IReadOnlyList<string>? propertyPaths = null,
        bool includeAppState = true,
        bool tolerant = true) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        bool defaultNameSearch = names == null || names.Count == 0;
        IReadOnlyList<string> effectiveNames = defaultNameSearch ? DefaultConfigNames : names!;
        bool effectiveContains = defaultNameSearch || contains;
        List<HtmlJavaScriptConfigItem> items = new();

        AddJavaScriptConfigMatches(html, effectiveNames, effectiveContains, startsWith, propertyPaths, tolerant, defaultNameSearch, items);

        if (includeAppState) {
            foreach (HtmlAppStateEntry state in HtmlAppStateParser.Parse(html)) {
                if (!IsConfigNameMatch(state.Name, effectiveNames, effectiveContains, startsWith)) {
                    continue;
                }

                AddAppStateConfigMatches(state, propertyPaths, items);
            }
        }

        return items;
    }

    /// <summary>
    /// Compares stylesheet selectors with HTML markup and reports which selectors are used.
    /// </summary>
    /// <param name="html">HTML content to match against.</param>
    /// <param name="css">Optional CSS content. When omitted, inline style elements are used.</param>
    /// <param name="includeUnused">Includes selectors that did not match any elements.</param>
    /// <param name="maxMatchedElements">Maximum representative element selector hints returned for each CSS selector.</param>
    /// <returns>CSS usage records in stylesheet order.</returns>
    public static IReadOnlyList<HtmlStyleUsageItem> SelectStyleUsage(string html, string? css = null, bool includeUnused = true, int maxMatchedElements = 10) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (maxMatchedElements < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxMatchedElements), "The maximum number of matched elements cannot be negative.");
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<(string Css, string Source, int SourceIndex)> sources = new();
        if (!string.IsNullOrWhiteSpace(css)) {
            sources.Add((css!, "CssContent", 0));
        } else {
            int styleIndex = 0;
            foreach (IElement style in document.QuerySelectorAll("style")) {
                sources.Add((style.TextContent ?? string.Empty, "StyleElement", styleIndex++));
            }
        }

        List<HtmlStyleUsageItem> items = new();
        foreach ((string Css, string Source, int SourceIndex) source in sources) {
            foreach (HtmlCssRuleMatch rule in HtmlCssQueryParser.SelectRules(source.Css)) {
                HtmlStyleUsageItem usage = CreateStyleUsageItem(document, rule, source.Source, source.SourceIndex, items.Count, maxMatchedElements);
                if (includeUnused || usage.IsUsed || !string.IsNullOrEmpty(usage.Error)) {
                    items.Add(usage);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Finds form posts, hidden fields, tokens, inline endpoints, and optional linked-script endpoints in an HTML document.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="baseUri">Optional page URL used for linked script downloads and relative URL context.</param>
    /// <param name="includeLinkedScripts">Downloads and inspects linked JavaScript files when a base URI is available.</param>
    /// <param name="includeExternalLinkedScripts">Allows cross-origin linked JavaScript downloads.</param>
    /// <param name="client">Optional HTTP client reused for linked JavaScript downloads, including caller-specified proxy settings.</param>
    /// <returns>Interaction surface records in source-family order.</returns>
    public static async Task<IReadOnlyList<HtmlInteractionSurfaceItem>> FindInteractionSurfaceAsync(string html, Uri? baseUri = null, bool includeLinkedScripts = false, bool includeExternalLinkedScripts = false, HttpClient? client = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        List<HtmlInteractionSurfaceItem> items = new();
        Uri? effectiveBaseUri = GetEffectiveBaseUri(html, baseUri);
        foreach (HtmlFormResult form in HtmlParser.ParseFormsWithAngleSharp(html)) {
            string formName = FirstNonEmpty(form.Metadata.Id, $"form[{form.Metadata.FormIndex}]");
            string formUrl = ResolveUrlValue(form.Metadata.Action, effectiveBaseUri);
            AddSurface(items, "Form", formName, form.Metadata.Method.ToString().ToUpperInvariant(), formUrl, string.Empty, CreateFormSelector(form.Metadata), "Form", form.Metadata.FormIndex, false, string.Join(",", form.Fields.Select(static field => field.Name).Where(static name => !string.IsNullOrWhiteSpace(name))));
            foreach (HtmlFormField field in form.Fields.Where(static field => field.Type.ToString().Equals("Hidden", StringComparison.OrdinalIgnoreCase))) {
                AddSurface(items, "Field", field.Name, string.Empty, string.Empty, field.Value, $"{CreateFormSelector(form.Metadata)} input[name='{EscapeAttributeValue(field.Name)}']", "Field", form.Metadata.FormIndex, false, "hidden");
            }
        }

        foreach (HtmlToken token in HtmlTokenParser.Parse(html)) {
            AddSurface(items, "Token", token.Name, string.Empty, string.Empty, token.Value, token.Selector, token.Source, token.Index, false, string.Empty);
        }

        foreach (HtmlJavaScriptEndpoint endpoint in HtmlJavaScriptEndpointParser.ParseHtml(html)) {
            AddSurface(items, "Endpoint", FirstNonEmpty(endpoint.OperationName, endpoint.Client, endpoint.Url), endpoint.Method, endpoint.Url, string.Empty, FirstNonEmpty(endpoint.Selector, "script"), "InlineScript", endpoint.ScriptIndex ?? endpoint.Index, false, endpoint.Client);
        }

        if (includeLinkedScripts && effectiveBaseUri != null) {
            foreach (HtmlLinkedJavaScriptEndpoint endpoint in await HtmlLinkedJavaScriptEndpointParser.ParseAsync(html, effectiveBaseUri, includeExternalLinkedScripts, client).ConfigureAwait(false)) {
                string metadata = FirstNonEmpty(endpoint.Error, endpoint.OperationName, endpoint.Client, endpoint.ScriptUrl);
                AddSurface(items, "LinkedEndpoint", FirstNonEmpty(endpoint.OperationName, endpoint.Client, endpoint.Url, endpoint.ScriptUrl), endpoint.Method, endpoint.Url, string.Empty, endpoint.Selector, "LinkedScript", endpoint.ScriptIndex, endpoint.IsExternal, metadata);
            }
        }

        return items;
    }

    /// <summary>
    /// Compares original static HTML with rendered HTML using common parsing-friendly signatures.
    /// </summary>
    /// <param name="staticHtml">Original HTML before browser execution.</param>
    /// <param name="renderedHtml">HTML after browser execution.</param>
    /// <param name="baseUri">Optional page URL used to resolve links.</param>
    /// <returns>A static-vs-rendered comparison summary.</returns>
    public static HtmlStaticRenderedComparison CompareStaticRendered(string staticHtml, string renderedHtml, Uri? baseUri = null) {
        if (staticHtml == null) {
            throw new ArgumentNullException(nameof(staticHtml));
        }

        if (renderedHtml == null) {
            throw new ArgumentNullException(nameof(renderedHtml));
        }

        IReadOnlyList<HtmlDataItem> staticItems = SelectData(staticHtml, new[] { "JsonLd", "AppState", "ScriptData", "Token", "Form", "Link" }, baseUri);
        IReadOnlyList<HtmlDataItem> renderedItems = SelectData(renderedHtml, new[] { "JsonLd", "AppState", "ScriptData", "Token", "Form", "Link" }, baseUri);

        return new HtmlStaticRenderedComparison {
            StaticHtmlLength = staticHtml.Length,
            RenderedHtmlLength = renderedHtml.Length,
            StaticTextLength = GetTextLength(staticHtml),
            RenderedTextLength = GetTextLength(renderedHtml),
            StaticLinkCount = staticItems.Count(static item => item.Kind == "Link"),
            RenderedLinkCount = renderedItems.Count(static item => item.Kind == "Link"),
            StaticFormCount = staticItems.Count(static item => item.Kind == "Form"),
            RenderedFormCount = renderedItems.Count(static item => item.Kind == "Form"),
            StaticJsonLdCount = staticItems.Count(static item => item.Kind == "JsonLd"),
            RenderedJsonLdCount = renderedItems.Count(static item => item.Kind == "JsonLd"),
            Deltas = CreateDeltas(staticItems, renderedItems, baseUri)
        };
    }

    private static HtmlStyleUsageItem CreateStyleUsageItem(IDocument document, HtmlCssRuleMatch rule, string source, int sourceIndex, int index, int maxMatchedElements) {
        List<IElement> matches = new();
        string error = string.Empty;

        foreach (string selector in SplitSelectorList(rule.Selector)) {
            try {
                matches.AddRange(document.QuerySelectorAll(selector));
            } catch (Exception ex) when (ex is DomException || ex is FormatException || ex is ArgumentException) {
                error = FirstNonEmpty(error, ex.Message);
            }
        }

        string[] matchedElements = matches
            .Distinct()
            .Take(maxMatchedElements)
            .Select(CreateElementSelector)
            .ToArray();

        return new HtmlStyleUsageItem {
            Index = index,
            Selector = rule.Selector,
            IsUsed = matches.Count > 0,
            MatchCount = matches.Distinct().Count(),
            MatchedElements = matchedElements,
            CssText = rule.CssText,
            Context = rule.Context,
            Source = source,
            SourceIndex = sourceIndex,
            Error = error
        };
    }

    private static void AddJavaScriptConfigMatches(
        string html,
        IReadOnlyList<string> names,
        bool contains,
        bool startsWith,
        IReadOnlyList<string>? propertyPaths,
        bool tolerant,
        bool defaultNameSearch,
        List<HtmlJavaScriptConfigItem> items) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string type = script.GetAttribute("type") ?? string.Empty;
            if (!HtmlJavaScriptVariableSelector.IsJavaScriptScriptType(type)) {
                scriptIndex++;
                continue;
            }

            string content = script.TextContent ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) {
                scriptIndex++;
                continue;
            }

            IReadOnlyList<HtmlJavaScriptVariableMatch> matches;
            try {
                matches = HtmlJavaScriptVariableSelector.SelectJavaScript(content, names, contains, startsWith, false, propertyPaths, tolerant, scriptIndex, type, IsJavaScriptModuleType(type));
            } catch (Exception ex) when (IsRecoverableJavaScriptParseException(ex)) {
                scriptIndex++;
                continue;
            }

            HashSet<string>? seen = defaultNameSearch ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
            foreach (HtmlJavaScriptVariableMatch match in matches) {
                seen?.Add(CreateConfigMatchKey(match));
                AddJavaScriptConfigMatch(match, items);
            }

            if (defaultNameSearch) {
                AddCaseInsensitiveDefaultConfigMatches(content, names, propertyPaths, tolerant, scriptIndex, type, seen!, items);
            }

            scriptIndex++;
        }
    }

    private static void AddCaseInsensitiveDefaultConfigMatches(
        string content,
        IReadOnlyList<string> names,
        IReadOnlyList<string>? propertyPaths,
        bool tolerant,
        int scriptIndex,
        string scriptType,
        HashSet<string> seen,
        List<HtmlJavaScriptConfigItem> items) {
        IReadOnlyList<HtmlJavaScriptVariableMatch> matches = HtmlJavaScriptVariableSelector.SelectJavaScript(
            content,
            null,
            false,
            false,
            false,
            propertyPaths,
            tolerant,
            scriptIndex,
            scriptType,
            IsJavaScriptModuleType(scriptType));

        foreach (HtmlJavaScriptVariableMatch match in matches) {
            string key = CreateConfigMatchKey(match);
            if (seen.Contains(key)) {
                continue;
            }

            bool nameMatches = IsConfigNameMatch(match.Name, names, true, false);
            bool pathMatches = IsConfigNameMatch(match.Path, names, true, false);
            if (!nameMatches && !pathMatches) {
                continue;
            }

            seen.Add(key);
            AddJavaScriptConfigMatch(match, items);
        }
    }

    private static string CreateConfigMatchKey(HtmlJavaScriptVariableMatch match) =>
        $"{match.ScriptIndex}|{match.Name}|{match.Path}|{match.PropertyPath}";

    private static void AddJavaScriptConfigMatch(HtmlJavaScriptVariableMatch match, List<HtmlJavaScriptConfigItem> items) {
        items.Add(new HtmlJavaScriptConfigItem {
            Index = items.Count,
            Name = match.Name,
            Path = match.Path,
            Kind = match.Kind,
            PropertyPath = match.PropertyPath,
            Value = match.Value,
            RawValue = match.RawValue ?? SerializeValue(match.Value),
            ScriptIndex = match.ScriptIndex,
            ScriptType = match.ScriptType,
            Selector = match.ScriptIndex.HasValue ? $"script:nth-of-type({match.ScriptIndex.Value + 1})" : "script",
            Source = "JavaScript"
        });
    }

    private static bool IsRecoverableJavaScriptParseException(Exception exception) =>
        exception is ParseErrorException ||
        exception.GetType().Name.Equals("SyntaxErrorException", StringComparison.Ordinal);

    private static bool IsJavaScriptModuleType(string? type) =>
        (type ?? string.Empty).Split(';')[0].Trim().Equals("module", StringComparison.OrdinalIgnoreCase);

    private static void AddAppStateConfigMatches(HtmlAppStateEntry state, IReadOnlyList<string>? propertyPaths, List<HtmlJavaScriptConfigItem> items) {
        if (propertyPaths == null || propertyPaths.Count == 0) {
            AddAppStateConfigMatch(state, null, state.RawJson, state.RawJson, items);
            return;
        }

        object? parsedValue = ParseJsonValue(state.RawJson);
        foreach (string propertyPath in propertyPaths) {
            object? value = HtmlJavaScriptAstUtilities.GetPropertyPathValue(parsedValue, propertyPath);
            AddAppStateConfigMatch(state, propertyPath, value, SerializeValue(value), items);
        }
    }

    private static void AddAppStateConfigMatch(HtmlAppStateEntry state, string? propertyPath, object? value, string rawValue, List<HtmlJavaScriptConfigItem> items) {
        items.Add(new HtmlJavaScriptConfigItem {
            Index = items.Count,
            Name = state.Name,
            Path = state.Name,
            Kind = state.SourceKind,
            PropertyPath = propertyPath,
            Value = value,
            RawValue = rawValue,
            ScriptIndex = state.ScriptIndex,
            Selector = $"script:nth-of-type({state.ScriptIndex + 1})",
            Source = "AppState"
        });
    }

    private static IReadOnlyList<HtmlStaticRenderedDelta> CreateDeltas(IReadOnlyList<HtmlDataItem> staticItems, IReadOnlyList<HtmlDataItem> renderedItems, Uri? baseUri) {
        string[] kinds = { "Link", "Form", "JsonLd", "AppState", "ScriptData", "Token" };
        List<HtmlStaticRenderedDelta> deltas = new();
        foreach (string kind in kinds) {
            string[] staticSignatures = staticItems.Where(item => item.Kind == kind).Select(item => CreateSignature(item, baseUri)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] renderedSignatures = renderedItems.Where(item => item.Kind == kind).Select(item => CreateSignature(item, baseUri)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray();
            deltas.Add(new HtmlStaticRenderedDelta {
                Kind = kind,
                StaticCount = staticSignatures.Length,
                RenderedCount = renderedSignatures.Length,
                Added = renderedSignatures.Except(staticSignatures, StringComparer.OrdinalIgnoreCase).ToArray(),
                Removed = staticSignatures.Except(renderedSignatures, StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }

        return deltas;
    }

    private static string CreateSignature(HtmlDataItem item, Uri? baseUri) =>
        item.Kind.Equals("Form", StringComparison.OrdinalIgnoreCase)
            ? CreateFormSignature(item, baseUri)
            : item.Kind.Equals("Link", StringComparison.OrdinalIgnoreCase)
                ? CreateLinkSignature(item)
            : IsScriptBackedDataKind(item.Kind)
                ? CreateScriptBackedDataSignature(item)
            : $"{item.Kind}|{item.Name}|{item.Type}|{item.Id}|{item.RawValue}|{SerializeValue(item.Value)}|{item.Selector}";

    private static string CreateFormSignature(HtmlDataItem item, Uri? baseUri) =>
        $"{item.Kind}|{FirstNonEmpty(item.Id, IsPositionalFormName(item.Name) ? null : item.Name)}|{item.Type}|{ResolveUrlValue(item.RawValue, baseUri)}|{SerializeValue(item.Value)}";

    private static string CreateLinkSignature(HtmlDataItem item) =>
        $"{item.Kind}|{item.Name}|{item.Type}|{item.Id}|{SerializeValue(item.Value)}";

    private static string CreateScriptBackedDataSignature(HtmlDataItem item) =>
        $"{item.Kind}|{item.Name}|{item.Type}|{item.Id}|{item.RawValue}|{SerializeValue(item.Value)}";

    private static bool IsScriptBackedDataKind(string kind) =>
        kind.Equals("JsonLd", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("AppState", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("ScriptData", StringComparison.OrdinalIgnoreCase);

    private static bool IsPositionalFormName(string value) =>
        value.StartsWith("form[", StringComparison.OrdinalIgnoreCase) && value.EndsWith("]", StringComparison.Ordinal);

    private static Uri? GetEffectiveBaseUri(string html, Uri? baseUri) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = HtmlModernParserUtilities.GetEffectiveBaseUri(document, baseUri);
        if (effectiveBaseUri != null) {
            return effectiveBaseUri;
        }

        string? documentBase = document.QuerySelector("base[href]")?.GetAttribute("href");
        return Uri.TryCreate(documentBase, UriKind.Absolute, out Uri? absoluteBaseUri) ? absoluteBaseUri : null;
    }

    private static string ResolveUrlValue(string value, Uri? baseUri) {
        if (string.IsNullOrWhiteSpace(value) || baseUri == null) {
            return value;
        }

        return HtmlModernParserUtilities.ResolveUrl(value, baseUri);
    }

    private static int GetTextLength(string html) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return (document.Body?.TextContent ?? document.DocumentElement.TextContent ?? string.Empty).Trim().Length;
    }

    private static void Add(List<HtmlDataItem> items, string kind, string name, string? type, string? id, object? value, string rawValue, string selector, string source, int? sourceIndex) {
        items.Add(new HtmlDataItem {
            Index = items.Count,
            Kind = kind,
            Name = name,
            Type = type,
            Id = id,
            Value = value,
            RawValue = rawValue,
            Selector = selector,
            Source = source,
            SourceIndex = sourceIndex
        });
    }

    private static void AddSurface(List<HtmlInteractionSurfaceItem> items, string kind, string name, string method, string url, string value, string selector, string source, int? sourceIndex, bool isExternal, string metadata) {
        items.Add(new HtmlInteractionSurfaceItem {
            Index = items.Count,
            Kind = kind,
            Name = name,
            Method = method,
            Url = url,
            Value = value,
            Selector = selector,
            Source = source,
            SourceIndex = sourceIndex,
            IsExternal = isExternal,
            Metadata = metadata
        });
    }

    private static HashSet<string>? CreateKindFilter(IReadOnlyCollection<string>? kinds) {
        if (kinds == null || kinds.Count == 0) {
            return null;
        }

        return new HashSet<string>(kinds.Where(static kind => !string.IsNullOrWhiteSpace(kind)).Select(static kind => kind.Trim()), StringComparer.OrdinalIgnoreCase);
    }

    private static bool Includes(HashSet<string>? filter, string kind) => filter == null || filter.Contains(kind);

    private static bool IsConfigNameMatch(string value, IReadOnlyList<string> names, bool contains, bool startsWith) {
        if (names.Count == 0) {
            return true;
        }

        foreach (string name in names) {
            if (contains && value.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            if (startsWith && value.StartsWith(name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (!contains && !startsWith && value.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string FirstNonEmpty(params string?[] values) {
        foreach (string? value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value!.Trim();
            }
        }

        return string.Empty;
    }

    private static string SerializeValue(object? value) {
        if (value == null) {
            return string.Empty;
        }

        try {
            return JsonSerializer.Serialize(value);
        } catch (NotSupportedException) {
            return value.ToString() ?? string.Empty;
        }
    }

    private static object? ParseJsonValue(string json) {
        try {
            using JsonDocument document = JsonDocument.Parse(json, HtmlModernParserUtilities.JsonOptions);
            return ConvertJsonElement(document.RootElement);
        } catch (JsonException) {
            return json;
        }
    }

    private static object? ConvertJsonElement(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty property in element.EnumerateObject()) {
                    properties[property.Name] = ConvertJsonElement(property.Value);
                }
                return properties;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(ConvertJsonElement).ToList();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long integer)) {
                    return integer;
                }
                return element.TryGetDecimal(out decimal number) ? number : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static string[] SplitSelectorList(string selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return Array.Empty<string>();
        }

        List<string> selectors = new();
        int start = 0;
        int bracketDepth = 0;
        bool inString = false;
        char quote = '\0';
        for (int index = 0; index < selector.Length; index++) {
            char current = selector[index];
            if (inString) {
                if (current == '\\') {
                    index++;
                } else if (current == quote) {
                    inString = false;
                }

                continue;
            }

            if (current == '"' || current == '\'') {
                inString = true;
                quote = current;
                continue;
            }

            if (current == '[' || current == '(') {
                bracketDepth++;
                continue;
            }

            if ((current == ']' || current == ')') && bracketDepth > 0) {
                bracketDepth--;
                continue;
            }

            if (current == ',' && bracketDepth == 0) {
                AddSelectorPart(selectors, selector.Substring(start, index - start));
                start = index + 1;
            }
        }

        AddSelectorPart(selectors, selector.Substring(start));
        return selectors.ToArray();
    }

    private static void AddSelectorPart(List<string> selectors, string value) {
        string trimmed = value.Trim();
        if (trimmed.Length > 0) {
            selectors.Add(trimmed);
        }
    }

    private static string CreateElementSelector(IElement element) {
        string tag = element.LocalName;
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            return $"{tag}#{element.Id}";
        }

        string? name = element.GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name)) {
            return $"{tag}[name='{EscapeAttributeValue(name!)}']";
        }

        string? classes = element.GetAttribute("class");
        if (!string.IsNullOrWhiteSpace(classes)) {
            string classSelector = string.Join(".", classes!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(2));
            return classSelector.Length > 0 ? $"{tag}.{classSelector}" : tag;
        }

        return tag;
    }

    private static string CreateFormSelector(HtmlFormMetadata metadata) =>
        !string.IsNullOrWhiteSpace(metadata.Id)
            ? $"form#{metadata.Id}"
            : $"form:nth-of-type({metadata.FormIndex + 1})";

    private static string CreateAttributeSelector(string tag, string attribute, string value) =>
        string.IsNullOrWhiteSpace(value) ? tag : $"{tag}[{attribute}='{EscapeAttributeValue(value)}']";

    private static string EscapeAttributeValue(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
