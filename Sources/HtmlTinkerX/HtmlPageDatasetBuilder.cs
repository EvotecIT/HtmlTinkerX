using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HtmlTinkerX;

/// <summary>
/// Builds compact single-page dataset chunks from a page workbench result.
/// </summary>
public static class HtmlPageDatasetBuilder {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = false
    };

    /// <summary>
    /// Builds dataset chunks from a page workbench result.
    /// </summary>
    /// <param name="workbench">Page workbench result.</param>
    /// <param name="options">Dataset options.</param>
    /// <returns>Dataset chunks suitable for JSONL export.</returns>
    public static IReadOnlyList<HtmlPageDatasetChunk> Build(HtmlPageWorkbenchResult workbench, HtmlPageDatasetOptions? options = null) {
        if (workbench == null) {
            throw new ArgumentNullException(nameof(workbench));
        }

        HtmlPageDatasetOptions effectiveOptions = options ?? new HtmlPageDatasetOptions();
        if (effectiveOptions.MaxChunkWords < 50) {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxChunkWords must be at least 50.");
        }

        string sourceText = FirstNonEmpty(workbench.ReadableText?.Text, StripMarkdown(workbench.Markdown));
        List<string> chunkTexts = BuildTextChunks(sourceText, effectiveOptions.MaxChunkWords);
        if (chunkTexts.Count == 0) {
            chunkTexts.Add(string.Empty);
        }

        IReadOnlyList<string> headings = ExtractHeadings(workbench);
        IReadOnlyList<string> dataKinds = workbench.Data
            .Select(static item => item.Kind)
            .Where(static kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<string> redactionHints = effectiveOptions.IncludeRedactionHints
            ? BuildRedactionHints(workbench)
            : Array.Empty<string>();
        IReadOnlyList<HtmlPageDatasetProvenanceEntry> provenance = effectiveOptions.IncludeProvenance
            ? BuildProvenance(workbench)
            : Array.Empty<HtmlPageDatasetProvenanceEntry>();

        List<HtmlPageDatasetChunk> chunks = new();
        for (int index = 0; index < chunkTexts.Count; index++) {
            string text = chunkTexts[index];
            chunks.Add(new HtmlPageDatasetChunk {
                ChunkId = $"page-chunk-{index + 1:D4}",
                ChunkIndex = index,
                SourceUrl = workbench.SourceUrl,
                FinalUrl = workbench.FinalUrl,
                Title = workbench.Title,
                AnalysisMode = workbench.AnalysisMode,
                Text = text,
                Markdown = effectiveOptions.IncludeMarkdown ? SelectMarkdownSlice(workbench.Markdown, text) : string.Empty,
                Summary = BuildSummary(text),
                WordCount = CountWords(text),
                CharacterCount = text.Length,
                Headings = headings,
                DataKinds = dataKinds,
                FormCount = workbench.FormCount,
                EndpointCount = workbench.EndpointCount,
                RedactionHints = redactionHints,
                Provenance = provenance
            });
        }

        return chunks;
    }

    /// <summary>
    /// Serializes dataset chunks as JSON Lines.
    /// </summary>
    /// <param name="chunks">Dataset chunks.</param>
    /// <returns>JSONL string.</returns>
    public static string ToJsonLines(IEnumerable<HtmlPageDatasetChunk> chunks) {
        if (chunks == null) {
            throw new ArgumentNullException(nameof(chunks));
        }

        return string.Join(Environment.NewLine, chunks.Select(chunk => JsonSerializer.Serialize(chunk, JsonOptions)));
    }

    private static List<string> BuildTextChunks(string text, int maxChunkWords) {
        string[] words = (text ?? string.Empty)
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> chunks = new();
        for (int start = 0; start < words.Length; start += maxChunkWords) {
            string chunk = string.Join(" ", words.Skip(start).Take(maxChunkWords));
            if (!string.IsNullOrWhiteSpace(chunk)) {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private static IReadOnlyList<string> ExtractHeadings(HtmlPageWorkbenchResult workbench) {
        string html = FirstNonEmpty(workbench.RenderedSnapshot?.Html, workbench.Html);
        if (string.IsNullOrWhiteSpace(html)) {
            return Array.Empty<string>();
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return document.QuerySelectorAll("h1,h2,h3")
            .Select(static heading => NormalizeWhitespace(heading.TextContent))
            .Where(static heading => !string.IsNullOrWhiteSpace(heading))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildRedactionHints(HtmlPageWorkbenchResult workbench) {
        List<string> hints = new();
        if (workbench.HiddenFieldCount > 0) {
            hints.Add("hidden-form-fields");
        }

        if (workbench.Tokens.Count > 0) {
            hints.Add("token-surfaces");
        }

        if (workbench.ExtractionPlan?.HasLoginForm == true) {
            hints.Add("login-form");
        }

        if (workbench.ExtractionPlan?.HasAutoSubmitForm == true) {
            hints.Add("browserless-auth-relay");
        }

        foreach (string warning in workbench.Warnings) {
            if (warning.Contains("sensitive", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("token", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("auth", StringComparison.OrdinalIgnoreCase)) {
                hints.Add("sensitive-warning");
                break;
            }
        }

        return hints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<HtmlPageDatasetProvenanceEntry> BuildProvenance(HtmlPageWorkbenchResult workbench) {
        List<HtmlPageDatasetProvenanceEntry> provenance = new() {
            new HtmlPageDatasetProvenanceEntry {
                Kind = "ReadableText",
                Name = FirstNonEmpty(workbench.ReadableText?.Title, workbench.Title),
                Selector = workbench.ReadableText?.SelectorHint ?? string.Empty,
                Url = FirstNonEmpty(workbench.FinalUrl, workbench.SourceUrl),
                Source = workbench.AnalysisMode
            }
        };

        foreach (HtmlDataItem item in workbench.Data.Take(40)) {
            provenance.Add(new HtmlPageDatasetProvenanceEntry {
                Kind = item.Kind,
                Name = item.Name,
                Selector = item.Selector,
                Url = item.RawValue,
                Source = item.Source
            });
        }

        foreach (HtmlInteractionSurfaceItem item in workbench.InteractionSurface.Take(40)) {
            provenance.Add(new HtmlPageDatasetProvenanceEntry {
                Kind = item.Kind,
                Name = item.Name,
                Selector = item.Selector,
                Url = item.Url,
                Source = item.Source
            });
        }

        return provenance;
    }

    private static string SelectMarkdownSlice(string markdown, string text) =>
        string.IsNullOrWhiteSpace(markdown) ? string.Empty : markdown;

    private static string BuildSummary(string text) {
        string normalized = NormalizeWhitespace(text);
        if (normalized.Length <= 240) {
            return normalized;
        }

        return normalized.Substring(0, 240).TrimEnd() + "...";
    }

    private static string StripMarkdown(string markdown) =>
        NormalizeWhitespace((markdown ?? string.Empty)
            .Replace("#", " ")
            .Replace("*", " ")
            .Replace("`", " "));

    private static int CountWords(string text) =>
        (text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string NormalizeWhitespace(string value) =>
        string.Join(" ", (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
