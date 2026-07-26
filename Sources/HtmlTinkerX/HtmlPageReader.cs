using OfficeIMO.Html;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Creates an OfficeIMO-style object graph from HTML and enriches it with web-page collections.
/// </summary>
public static class HtmlPageReader {
    /// <summary>
    /// Parses HTML into headings, paragraphs, tables, links, resources, and inferred repeated collections.
    /// </summary>
    /// <param name="html">HTML markup to read.</param>
    /// <param name="options">Optional page reader settings.</param>
    /// <returns>An object-first page document.</returns>
    public static HtmlPageDocument Read(string html, HtmlPageReaderOptions? options = null) {
        if (html == null) throw new ArgumentNullException(nameof(html));
        HtmlPageReaderOptions effective = options ?? new HtmlPageReaderOptions();
        if (effective.MinimumRepeatCount < 2) {
            throw new ArgumentOutOfRangeException("MinimumRepeatCount");
        }

        if (effective.CollectionLimit <= 0 || effective.CollectionLimit > 100) {
            throw new ArgumentOutOfRangeException("CollectionLimit", "Collection limit must be between 1 and 100.");
        }

        HtmlConversionDocumentOptions conversionOptions =
            (effective.ConversionOptions ?? HtmlConversionDocumentOptions.CreateUntrustedProfile()).Clone();
        conversionOptions.BaseUri ??= effective.BaseUri ?? effective.FinalUri ?? effective.SourceUri;
        if (effective.ConversionOptions == null) conversionOptions.IncludeNormalizedHtml = false;
        HtmlConversionDocument content = HtmlConversionDocument.Parse(html, conversionOptions);
        HtmlSemanticDocument semantic = content.SemanticDocument;
        HtmlSemanticBlock[] blocks = FlattenBlocks(semantic.Sections.SelectMany(static section => section.Blocks)).ToArray();
        HtmlReadableTextResult readableText = HtmlParserToText.ExtractReadableText(html);
        Uri? effectiveBaseUri = content.BaseUri ?? conversionOptions.BaseUri;
        IReadOnlyList<HtmlDataItem> data = HtmlParsingToolbox.SelectData(html, baseUri: effectiveBaseUri);
        IReadOnlyList<HtmlPageCollection> collections = effective.IncludeCollections
            ? HtmlDomExtraction.DiscoverCollections(
                html,
                effective.CollectionHint,
                effectiveBaseUri,
                effective.MinimumRepeatCount,
                effective.CollectionLimit)
            : Array.Empty<HtmlPageCollection>();

        return new HtmlPageDocument {
            SourceUrl = effective.SourceUri?.AbsoluteUri ?? string.Empty,
            FinalUrl = (effective.FinalUri ?? effective.SourceUri)?.AbsoluteUri ?? string.Empty,
            EffectiveBaseUrl = effectiveBaseUri?.AbsoluteUri ?? string.Empty,
            AnalysisMode = string.IsNullOrWhiteSpace(effective.AnalysisMode) ? "Static" : effective.AnalysisMode,
            Title = FirstNonEmpty(semantic.Title, readableText.Title),
            Content = content,
            ReadableText = readableText,
            Markdown = HtmlMarkdownConverterAdapter.ConvertToMarkdown(content, effectiveBaseUri?.AbsoluteUri),
            Blocks = blocks,
            Headings = blocks.Where(static block => block.Kind == HtmlSemanticBlockKind.Heading).ToArray(),
            Paragraphs = blocks.Where(static block => block.Kind == HtmlSemanticBlockKind.Paragraph).ToArray(),
            Lists = blocks.Where(static block => block.Kind == HtmlSemanticBlockKind.List).ToArray(),
            Tables = blocks
                .Where(static block => block.Kind == HtmlSemanticBlockKind.Table && block.Table != null)
                .Select(static block => block.Table!)
                .ToArray(),
            Links = FilterData(data, "Link")
                .Select(static item => new HtmlPageLink {
                    Index = item.Index,
                    Text = item.Name,
                    Url = item.Value?.ToString() ?? item.RawValue,
                    RawUrl = item.RawValue,
                    Selector = item.Selector
                })
                .ToArray(),
            Forms = FilterData(data, "Form"),
            Assets = FilterData(data, "Asset"),
            Collections = collections
        };
    }

    private static IEnumerable<HtmlSemanticBlock> FlattenBlocks(IEnumerable<HtmlSemanticBlock> blocks) {
        foreach (HtmlSemanticBlock block in blocks) {
            yield return block;
            foreach (HtmlSemanticBlock child in FlattenBlocks(block.Children)) yield return child;
        }
    }

    private static IReadOnlyList<HtmlDataItem> FilterData(IEnumerable<HtmlDataItem> data, string kind) =>
        data.Where(item => item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).ToArray();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
