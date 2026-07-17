using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;

namespace HtmlTinkerX;

/// <summary>
/// Builds one-page intelligence results from reusable HtmlTinkerX parsers.
/// </summary>
public static class HtmlPageWorkbench {
    /// <summary>
    /// Analyzes one HTML page and returns normalized extraction, interaction, and next-step guidance.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="options">Workbench options.</param>
    /// <param name="client">Optional HTTP client used for linked-script inspection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A composed page workbench result.</returns>
    public static async Task<HtmlPageWorkbenchResult> AnalyzeAsync(
        string html,
        HtmlPageWorkbenchOptions? options = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlPageWorkbenchOptions effectiveOptions = options ?? new HtmlPageWorkbenchOptions();
        Uri? baseUri = effectiveOptions.BaseUri;
        IDocument staticDocument = await HtmlParser.ParseWithAngleSharpAsync(html, cancellationToken).ConfigureAwait(false);
        Uri? staticEffectiveBaseUri = HtmlModernParserUtilities.GetEffectiveBaseUri(staticDocument, baseUri);
        HtmlReadableTextResult staticReadableText = HtmlParserToText.ExtractReadableText(html);
        cancellationToken.ThrowIfCancellationRequested();
        string staticMarkdown = HtmlParserToMarkdown.ConvertToMarkdown(html, staticEffectiveBaseUri?.AbsoluteUri);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<HtmlDataItem> staticData = HtmlParsingToolbox.SelectData(html, baseUri: baseUri);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<HtmlJavaScriptConfigItem> staticJavaScriptConfig = HtmlParsingToolbox.SelectJavaScriptConfig(html);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<HtmlInteractionSurfaceItem> staticInteractionSurface = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            baseUri,
            effectiveOptions.IncludeLinkedScripts,
            effectiveOptions.IncludeExternalLinkedScripts,
            client,
            cancellationToken).ConfigureAwait(false);

        HtmlRenderedPageSnapshot? renderedSnapshot = effectiveOptions.RenderedSnapshot;
        bool hasRenderedSnapshot = renderedSnapshot != null && !string.IsNullOrWhiteSpace(renderedSnapshot.Html);
        Uri? renderedBaseUri = GetRenderedBaseUri(renderedSnapshot, baseUri);
        IDocument? renderedDocument = hasRenderedSnapshot
            ? await HtmlParser.ParseWithAngleSharpAsync(renderedSnapshot!.Html, cancellationToken).ConfigureAwait(false)
            : null;
        Uri? renderedEffectiveBaseUri = renderedDocument == null
            ? null
            : HtmlModernParserUtilities.GetEffectiveBaseUri(renderedDocument, renderedBaseUri);
        IReadOnlyList<HtmlDataItem> renderedData = hasRenderedSnapshot
            ? NormalizeList(renderedSnapshot!.Data, () => HtmlParsingToolbox.SelectData(renderedSnapshot.Html, baseUri: renderedBaseUri))
            : Array.Empty<HtmlDataItem>();
        IReadOnlyList<HtmlInteractionSurfaceItem> renderedInteractionSurface = hasRenderedSnapshot
            ? await GetRenderedInteractionSurfaceAsync(renderedSnapshot!, renderedBaseUri, effectiveOptions, client, cancellationToken).ConfigureAwait(false)
            : Array.Empty<HtmlInteractionSurfaceItem>();
        IReadOnlyList<HtmlJavaScriptConfigItem> renderedJavaScriptConfig = hasRenderedSnapshot
            ? NormalizeList(renderedSnapshot!.JavaScriptConfig, () => HtmlParsingToolbox.SelectJavaScriptConfig(renderedSnapshot.Html))
            : Array.Empty<HtmlJavaScriptConfigItem>();
        HtmlReadableTextResult readableText = hasRenderedSnapshot
            ? renderedSnapshot!.ReadableText ?? HtmlParserToText.ExtractReadableText(renderedSnapshot.Html)
            : staticReadableText;
        string markdown = hasRenderedSnapshot
            ? FirstNonEmpty(renderedSnapshot!.Markdown, HtmlParserToMarkdown.ConvertToMarkdown(renderedSnapshot.Html, renderedEffectiveBaseUri?.AbsoluteUri))
            : staticMarkdown;
        IReadOnlyList<HtmlDataItem> data = hasRenderedSnapshot ? renderedData : staticData;
        IReadOnlyList<HtmlJavaScriptConfigItem> javaScriptConfig = hasRenderedSnapshot ? renderedJavaScriptConfig : staticJavaScriptConfig;
        IReadOnlyList<HtmlInteractionSurfaceItem> interactionSurface = hasRenderedSnapshot ? renderedInteractionSurface : staticInteractionSurface;
        HtmlStaticRenderedComparison? staticRenderedComparison = CreateStaticRenderedComparison(html, renderedSnapshot, renderedBaseUri, effectiveOptions);
        cancellationToken.ThrowIfCancellationRequested();
        IDocument analysisDocument = renderedDocument ?? staticDocument;
        string analysisHtml = hasRenderedSnapshot ? renderedSnapshot!.Html : html;
        Uri? analysisBaseUri = hasRenderedSnapshot ? renderedBaseUri : baseUri;
        HtmlDocumentAuditResult? documentAudit = effectiveOptions.IncludeDocumentAudit
            ? HtmlDocumentAudit.Analyze(analysisDocument, effectiveOptions.DocumentAuditOptions, cancellationToken)
            : null;
        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(
            analysisHtml,
            analysisDocument,
            readableText,
            data,
            analysisBaseUri,
            cancellationToken);

        IReadOnlyList<HtmlDataItem> forms = FilterData(data, "Form");
        IReadOnlyList<HtmlDataItem> links = FilterData(data, "Link");
        IReadOnlyList<HtmlDataItem> assets = FilterData(data, "Asset");
        IReadOnlyList<HtmlDataItem> jsonLd = FilterData(data, "JsonLd");
        IReadOnlyList<HtmlDataItem> openGraph = FilterData(data, "OpenGraph");
        IReadOnlyList<HtmlDataItem> appState = FilterData(data, "AppState");
        IReadOnlyList<HtmlInteractionSurfaceItem> hiddenFields = FilterSurface(interactionSurface, "Field");
        IReadOnlyList<HtmlInteractionSurfaceItem> tokens = FilterSurface(interactionSurface, "Token");
        IReadOnlyList<HtmlInteractionSurfaceItem> endpoints = interactionSurface
            .Where(IsEndpointSurface)
            .ToArray();
        IReadOnlyList<string> warnings = CreateWarnings(plan, hiddenFields, tokens, endpoints, renderedSnapshot, staticRenderedComparison);

        HtmlPageWorkbenchResult result = new() {
            SourceUrl = baseUri?.AbsoluteUri ?? string.Empty,
            FinalUrl = FirstNonEmpty(renderedSnapshot?.FinalUrl, renderedSnapshot?.Url, baseUri?.AbsoluteUri),
            EffectiveBaseUrl = (hasRenderedSnapshot ? renderedEffectiveBaseUri : staticEffectiveBaseUri)?.AbsoluteUri ?? string.Empty,
            AnalysisMode = hasRenderedSnapshot ? "RenderedSnapshot" : "Static",
            Title = FirstNonEmpty(renderedSnapshot?.Title, readableText.Title, plan.Title),
            Html = effectiveOptions.IncludeHtml ? html : string.Empty,
            ReadableText = readableText,
            Markdown = markdown,
            ExtractionPlan = plan,
            RenderedSnapshot = renderedSnapshot,
            StaticRenderedComparison = staticRenderedComparison,
            DocumentAudit = documentAudit,
            SuggestedNextCommand = plan.SuggestedCommand,
            Warnings = warnings,
            Data = data,
            StaticData = staticData,
            RenderedData = renderedData,
            Forms = forms,
            Links = links,
            Assets = assets,
            JsonLd = jsonLd,
            OpenGraph = openGraph,
            AppState = appState,
            JavaScriptConfig = javaScriptConfig,
            InteractionSurface = interactionSurface,
            StaticInteractionSurface = staticInteractionSurface,
            RenderedInteractionSurface = renderedInteractionSurface,
            HiddenFields = hiddenFields,
            Tokens = tokens,
            Endpoints = endpoints,
            DataItemCount = data.Count,
            FormCount = forms.Count,
            HiddenFieldCount = hiddenFields.Count,
            LinkCount = links.Count,
            AssetCount = assets.Count,
            EndpointCount = endpoints.Count,
            JavaScriptConfigCount = javaScriptConfig.Count
        };

        IReadOnlyList<HtmlApiEndpointRecord> apiEndpoints = HtmlApiEndpointInventory.Build(result);
        result.ApiEndpoints = apiEndpoints;
        result.ApiEndpointCount = apiEndpoints.Count;
        return result;
    }

    private static IReadOnlyList<HtmlDataItem> FilterData(IEnumerable<HtmlDataItem> data, string kind) =>
        data.Where(item => item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).ToArray();

    private static IReadOnlyList<HtmlInteractionSurfaceItem> FilterSurface(IEnumerable<HtmlInteractionSurfaceItem> items, string kind) =>
        items.Where(item => item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).ToArray();

    private static bool IsEndpointSurface(HtmlInteractionSurfaceItem item) {
        if (item.Kind.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return item.Kind.Equals("LinkedEndpoint", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.Url);
    }

    private static IReadOnlyList<string> CreateWarnings(
        HtmlExtractionPlan plan,
        IReadOnlyList<HtmlInteractionSurfaceItem> hiddenFields,
        IReadOnlyList<HtmlInteractionSurfaceItem> tokens,
        IReadOnlyList<HtmlInteractionSurfaceItem> endpoints,
        HtmlRenderedPageSnapshot? renderedSnapshot,
        HtmlStaticRenderedComparison? staticRenderedComparison) {
        List<string> warnings = new(plan.Warnings);
        if (hiddenFields.Count > 0 || tokens.Count > 0) {
            warnings.Add("Hidden fields and token surfaces may contain sensitive values; avoid logging raw workbench output in shared channels.");
        }

        if (endpoints.Any(static endpoint => endpoint.IsExternal)) {
            warnings.Add("External linked-script endpoints were discovered; validate origin and trust boundaries before replaying requests.");
        }

        if (renderedSnapshot != null && staticRenderedComparison != null && HasRenderedDelta(staticRenderedComparison)) {
            warnings.Add("Rendered content differs from static HTML; use rendered workbench data for dynamic-page extraction decisions.");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static HtmlStaticRenderedComparison? CreateStaticRenderedComparison(
        string staticHtml,
        HtmlRenderedPageSnapshot? renderedSnapshot,
        Uri? renderedBaseUri,
        HtmlPageWorkbenchOptions options) {
        if (renderedSnapshot == null || string.IsNullOrWhiteSpace(renderedSnapshot.Html) || !options.IncludeStaticRenderedComparison) {
            return renderedSnapshot?.StaticRenderedComparison;
        }

        return renderedSnapshot.StaticRenderedComparison
            ?? HtmlParsingToolbox.CompareStaticRendered(staticHtml, renderedSnapshot.Html, renderedBaseUri);
    }

    private static Uri? GetRenderedBaseUri(HtmlRenderedPageSnapshot? renderedSnapshot, Uri? fallback) {
        if (renderedSnapshot == null) {
            return fallback;
        }

        if (Uri.TryCreate(FirstNonEmpty(renderedSnapshot.FinalUrl, renderedSnapshot.Url), UriKind.Absolute, out Uri? uri)) {
            return uri;
        }

        return fallback;
    }

    private static IReadOnlyList<T> NormalizeList<T>(IReadOnlyList<T>? source, Func<IReadOnlyList<T>> fallback) =>
        source == null || source.Count == 0 ? fallback() : source;

    private static async Task<IReadOnlyList<HtmlInteractionSurfaceItem>> GetRenderedInteractionSurfaceAsync(
        HtmlRenderedPageSnapshot renderedSnapshot,
        Uri? renderedBaseUri,
        HtmlPageWorkbenchOptions options,
        HttpClient? client,
        CancellationToken cancellationToken) {
        if (options.IncludeLinkedScripts) {
            return await HtmlParsingToolbox.FindInteractionSurfaceAsync(
                renderedSnapshot.Html,
                renderedBaseUri,
                includeLinkedScripts: true,
                includeExternalLinkedScripts: options.IncludeExternalLinkedScripts,
                client,
                cancellationToken).ConfigureAwait(false);
        }

        return NormalizeList(
            renderedSnapshot.InteractionSurface,
            () => HtmlParsingToolbox.FindInteractionSurface(renderedSnapshot.Html, renderedBaseUri, renderedSnapshot.LinkedJavaScriptEndpoints));
    }

    private static bool HasRenderedDelta(HtmlStaticRenderedComparison comparison) =>
        comparison.Deltas.Any(static delta => delta.Added.Length > 0 || delta.Removed.Length > 0)
        || comparison.StaticTextLength != comparison.RenderedTextLength
        || comparison.StaticLinkCount != comparison.RenderedLinkCount
        || comparison.StaticFormCount != comparison.RenderedFormCount
        || comparison.StaticJsonLdCount != comparison.RenderedJsonLdCount;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
