using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    private static readonly Regex CollectionNameWords =
        new(@"[a-zA-Z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Infers distinct repeated record sets and extracts their fields without requiring selectors.
    /// </summary>
    /// <param name="html">HTML markup to inspect.</param>
    /// <param name="query">Optional visible text or URL fragment used to focus discovery.</param>
    /// <param name="baseUri">Optional page URL used to resolve relative URLs.</param>
    /// <param name="minimumRepeatCount">Minimum number of repeated items.</param>
    /// <param name="limit">Maximum number of distinct collections.</param>
    /// <returns>Ranked object collections with extracted items.</returns>
    public static IReadOnlyList<HtmlPageCollection> DiscoverCollections(
        string html,
        string? query = null,
        Uri? baseUri = null,
        int minimumRepeatCount = 2,
        int limit = 5) {
        if (html == null) throw new ArgumentNullException(nameof(html));
        if (minimumRepeatCount < 2) throw new ArgumentOutOfRangeException(nameof(minimumRepeatCount));
        if (limit <= 0 || limit > 100) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Collection limit must be between 1 and 100.");
        }

        int candidateLimit = Math.Max(limit * 4, 20);
        IReadOnlyList<HtmlDomSelectorCandidate> candidates = DiscoverSelectors(
            html,
            query,
            baseUri,
            minimumRepeatCount,
            candidateLimit);
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        HashSet<string> seenItemSets = new(StringComparer.Ordinal);
        HashSet<string> seenValueSets = new(StringComparer.Ordinal);
        List<HtmlPageCollection> collections = new(limit);

        foreach (HtmlDomSelectorCandidate candidate in candidates) {
            IElement[] elements;
            try {
                elements = document.QuerySelectorAll(candidate.Selector).ToArray();
            } catch {
                continue;
            }

            string itemSetKey = BuildItemSetKey(elements);
            if (!seenItemSets.Add(itemSetKey)) continue;

            Dictionary<string, HtmlDomFieldDefinition> fields =
                candidate.Fields.ToDictionary(
                    static field => field.Name,
                    static field => new HtmlDomFieldDefinition {
                        Selector = field.Selector,
                        Attribute = string.IsNullOrWhiteSpace(field.Attribute) ? null : field.Attribute,
                        All = field.MultiplePerItem,
                        ResolveUrl = IsUriAttribute(field.Attribute)
                    },
                    StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<HtmlDomExtractionRecord> records =
                Extract(document, candidate.Selector, fields, effectiveBaseUri);
            string valueSetKey = BuildValueSetKey(records, candidate.Fields);
            if (valueSetKey.Length > 0 && !seenValueSets.Add(valueSetKey)) continue;

            collections.Add(new HtmlPageCollection {
                Index = collections.Count,
                Name = BuildCollectionName(candidate),
                Confidence = candidate.Score >= 120 ? "High" : candidate.Score >= 80 ? "Medium" : "Low",
                Score = candidate.Score,
                Reason = candidate.Reason,
                Selector = candidate.Selector,
                Fields = candidate.Fields,
                Items = records.Select(static record => new HtmlPageCollectionItem {
                    Index = record.Index,
                    Values = record.Values
                }).ToArray()
            });

            if (collections.Count == limit) break;
        }

        return collections;
    }

    private static bool IsUriAttribute(string attribute) =>
        UriAttributes.Contains(attribute ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static string BuildItemSetKey(IEnumerable<IElement> elements) =>
        string.Join(
            ",",
            elements.Select(static element => {
                ISourceReference? source = element.SourceReference;
                return source == null
                    ? element.OuterHtml
                    : source.Position.Index.ToString(CultureInfo.InvariantCulture);
            }));

    private static string BuildValueSetKey(
        IReadOnlyList<HtmlDomExtractionRecord> records,
        IReadOnlyList<HtmlDomSelectorFieldCandidate> fields) {
        HtmlDomSelectorFieldCandidate? identityField =
            fields.FirstOrDefault(static field => field.Name.Equals("ProductLink", StringComparison.OrdinalIgnoreCase))
            ?? fields.FirstOrDefault(static field => field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase))
            ?? fields.FirstOrDefault(static field =>
                field.Name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0
                || field.Name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0);
        if (identityField == null) return string.Empty;

        string[] values = records
            .Select(record => record.Values.TryGetValue(identityField.Name, out object? value)
                ? FormatIdentityValue(value)
                : string.Empty)
            .ToArray();
        return values.Any(static value => value.Length > 0)
            ? identityField.Attribute + ":" + string.Join("\u001f", values)
            : string.Empty;
    }

    private static string FormatIdentityValue(object? value) {
        if (value is IEnumerable<object?> values) {
            return string.Join("\u001e", values.Select(static item => item?.ToString() ?? string.Empty));
        }

        return value?.ToString() ?? string.Empty;
    }

    private static string BuildCollectionName(HtmlDomSelectorCandidate candidate) {
        if (candidate.Tag.Equals("tr", StringComparison.OrdinalIgnoreCase)) return "Table Rows";
        if (candidate.Tag.Equals("li", StringComparison.OrdinalIgnoreCase)) return "List Items";

        string classPart = candidate.Selector
            .Split('.')
            .Skip(1)
            .Select(static part => part.Split('[', ':', '>', ' ')[0])
            .FirstOrDefault(static part => !string.IsNullOrWhiteSpace(part))
            ?? string.Empty;
        string source = classPart.Length > 0 ? classPart : candidate.Tag;
        string words = string.Join(
            " ",
            CollectionNameWords.Matches(source)
                .Cast<Match>()
                .Select(static match => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(match.Value.ToLowerInvariant())));
        if (words.Length == 0) words = "Items";
        if (!words.EndsWith("s", StringComparison.OrdinalIgnoreCase)) words += "s";
        return words;
    }
}
