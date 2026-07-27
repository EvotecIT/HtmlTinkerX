using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// CSS-first static DOM selection, repeated-record extraction, and selector discovery.
/// </summary>
public static partial class HtmlDomExtraction {
    private static readonly string[] CandidateTags = { "article", "div", "li", "section", "tr" };
    private static readonly string[] UriAttributes = {
        "href", "src", "action", "poster", "data-src", "data-lazy-src", "data-original",
        "srcset", "data-srcset", "data-lazy-srcset", "data-original-srcset"
    };
    private static readonly string[] MediaSourceAttributes = {
        "data-src", "data-lazy-src", "data-original",
        "data-srcset", "data-lazy-srcset", "data-original-srcset", "srcset", "src"
    };
    private static readonly string[] SourceSetAttributes = {
        "srcset", "data-srcset", "data-lazy-srcset", "data-original-srcset"
    };
    private static readonly string[] SemanticAttributes = { "data-type", "itemprop", "name", "role" };
    private static readonly Regex SafeCssIdentifier = new(@"^-?[_a-zA-Z]+[_a-zA-Z0-9-]*$", RegexOptions.Compiled);
    private static readonly Regex PropertyTokenSplit = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Selects elements with a CSS selector from static HTML.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <param name="selector">CSS selector to evaluate.</param>
    /// <param name="first">Return only the first matching element.</param>
    /// <returns>Matching AngleSharp elements in document order.</returns>
    public static IReadOnlyList<IElement> SelectElements(string html, string selector, bool first = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("CSS selector cannot be empty.", nameof(selector));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        try {
            if (first) {
                IElement? match = document.QuerySelector(selector);
                return match == null ? Array.Empty<IElement>() : new[] { match };
            }

            return document.QuerySelectorAll(selector).ToArray();
        } catch (Exception exception) {
            throw new ArgumentException($"CSS selector '{selector}' is invalid: {exception.Message}", nameof(selector), exception);
        }
    }

    /// <summary>
    /// Extracts property dictionaries from repeated elements selected with CSS.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <param name="itemSelector">CSS selector matching each repeated item.</param>
    /// <param name="properties">Property definitions evaluated relative to each item.</param>
    /// <param name="baseUri">Optional page URL used to resolve relative URL attributes.</param>
    /// <returns>One extraction record per selected item.</returns>
    public static IReadOnlyList<HtmlDomExtractionRecord> Extract(
        string html,
        string itemSelector,
        IReadOnlyDictionary<string, HtmlDomFieldDefinition> properties,
        Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (string.IsNullOrWhiteSpace(itemSelector)) {
            throw new ArgumentException("Item selector cannot be empty.", nameof(itemSelector));
        }

        if (properties == null || properties.Count == 0) {
            throw new ArgumentException("At least one property definition is required.", nameof(properties));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return Extract(
            document,
            itemSelector,
            properties,
            GetEffectiveBaseUri(document, baseUri));
    }

    private static IReadOnlyList<HtmlDomExtractionRecord> Extract(
        IDocument document,
        string itemSelector,
        IReadOnlyDictionary<string, HtmlDomFieldDefinition> properties,
        Uri? effectiveBaseUri) {
        IHtmlCollection<IElement> items;
        try {
            items = document.QuerySelectorAll(itemSelector);
        } catch (Exception exception) {
            throw new ArgumentException($"Item selector '{itemSelector}' is invalid: {exception.Message}", nameof(itemSelector), exception);
        }

        List<HtmlDomExtractionRecord> records = new(items.Length);
        for (int index = 0; index < items.Length; index++) {
            IElement item = items[index];
            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HtmlDomFieldDefinition> property in properties) {
                if (string.IsNullOrWhiteSpace(property.Key)) {
                    throw new ArgumentException("Property names cannot be empty.", nameof(properties));
                }

                HtmlDomFieldDefinition definition = property.Value
                    ?? throw new ArgumentException($"Property '{property.Key}' has no field definition.", nameof(properties));
                values[property.Key] = ExtractFieldValue(item, definition, property.Key, index, effectiveBaseUri);
            }

            records.Add(new HtmlDomExtractionRecord {
                Index = index,
                ItemSelector = itemSelector,
                Values = values
            });
        }

        return records;
    }

    /// <summary>
    /// Finds and ranks repeated static DOM structures, their likely fields, and link attributes.
    /// </summary>
    /// <param name="html">HTML markup to inspect.</param>
    /// <param name="query">Optional visible text, URL, id, class, or attribute fragment used to focus discovery.</param>
    /// <param name="baseUri">Optional page URL used for samples and the suggested command.</param>
    /// <param name="minimumRepeatCount">Minimum number of elements a candidate selector must match.</param>
    /// <param name="limit">Maximum number of ranked candidates to return.</param>
    /// <param name="commandSource">Optional source context used to build a replayable extraction command.</param>
    /// <returns>Ranked repeated-structure candidates.</returns>
    public static IReadOnlyList<HtmlDomSelectorCandidate> DiscoverSelectors(
        string html,
        string? query = null,
        Uri? baseUri = null,
        int minimumRepeatCount = 2,
        int limit = 10,
        HtmlDomCommandSource? commandSource = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (minimumRepeatCount < 2) {
            throw new ArgumentOutOfRangeException(nameof(minimumRepeatCount), "Minimum repeat count must be at least two.");
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        string normalizedQuery = NormalizeWhitespace(query);
        HashSet<string> selectors = new(StringComparer.Ordinal);

        foreach (IElement element in document.QuerySelectorAll(string.Join(",", CandidateTags))) {
            if (normalizedQuery.Length > 0 && !ElementOrDescendantMatchesQuery(element, normalizedQuery)) {
                continue;
            }

            foreach (string selector in BuildCandidateSelectors(element)) {
                selectors.Add(selector);
            }
        }

        List<HtmlDomSelectorCandidate> candidates = new();
        foreach (string selector in selectors) {
            IElement[] items;
            try {
                items = document.QuerySelectorAll(selector).ToArray();
            } catch {
                continue;
            }

            if (items.Length < minimumRepeatCount || items.Length > 250) {
                continue;
            }

            if (normalizedQuery.Length > 0 && !items.Any(item => ElementOrDescendantMatchesQuery(item, normalizedQuery))) {
                continue;
            }

            HtmlDomSelectorFieldCandidate[] fields = DiscoverFields(items, effectiveBaseUri);
            if (fields.Length < 2) {
                continue;
            }

            string sampleText = NormalizeWhitespace(items.FirstOrDefault()?.TextContent);
            if (sampleText.Length > 240) {
                sampleText = sampleText.Substring(0, 237) + "...";
            }

            int score = ScoreCollection(items, fields, normalizedQuery, selector);
            SuggestedCommandBuildResult suggested = BuildSuggestedCommand(
                selector,
                fields,
                commandSource ?? new HtmlDomCommandSource { BaseUri = baseUri });
            candidates.Add(new HtmlDomSelectorCandidate {
                Selector = selector,
                Tag = items[0].LocalName,
                MatchCount = items.Length,
                Score = score,
                Reason = BuildCollectionReason(items, fields, normalizedQuery),
                SampleText = sampleText,
                Fields = fields,
                SuggestedCommand = suggested.Command,
                SuggestedCommandIsReplayable = suggested.IsReplayable,
                SuggestedCommandNote = suggested.Note
            });
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.Fields.Length)
            .ThenBy(static candidate => candidate.MatchCount)
            .ThenBy(static candidate => candidate.Selector, StringComparer.Ordinal)
            .Take(limit)
            .Select((candidate, index) => {
                candidate.Index = index;
                return candidate;
            })
            .ToArray();
    }

    private static object? ExtractFieldValue(
        IElement item,
        HtmlDomFieldDefinition definition,
        string propertyName,
        int itemIndex,
        Uri? baseUri) {
        IElement[] matches;
        try {
            matches = string.IsNullOrWhiteSpace(definition.Selector)
                ? new[] { item }
                : item.QuerySelectorAll(definition.Selector).ToArray();
        } catch (Exception exception) {
            throw new ArgumentException(
                $"Selector '{definition.Selector}' for property '{propertyName}' is invalid: {exception.Message}",
                nameof(definition.Selector),
                exception);
        }

        object?[] values = matches
            .Select(match => ReadElementValue(match, definition, baseUri))
            .Where(static value => value != null)
            .ToArray();

        if (values.Length == 0) {
            if (definition.Required) {
                throw new InvalidOperationException(
                    $"Required property '{propertyName}' did not match item {itemIndex} using selector '{definition.Selector}'.");
            }

            return definition.All
                ? Array.Empty<object?>()
                : definition.DefaultValue;
        }

        return definition.All ? values : values[0];
    }

    private static object? ReadElementValue(IElement element, HtmlDomFieldDefinition definition, Uri? baseUri) {
        if (!string.IsNullOrWhiteSpace(definition.Attribute)) {
            string attributeName = definition.Attribute!;
            string? value = element.GetAttribute(attributeName);
            if (value == null) {
                return null;
            }

            if (SourceSetAttributes.Contains(attributeName, StringComparer.OrdinalIgnoreCase)) {
                value = HtmlImageCandidateParser.GetBestSourceSetSource(value);
            }

            bool shouldResolve = definition.ResolveUrl
                || UriAttributes.Contains(attributeName, StringComparer.OrdinalIgnoreCase);
            return shouldResolve ? ResolveUrl(value, baseUri) : value;
        }

        if (definition.ValueKind.Equals("Html", StringComparison.OrdinalIgnoreCase)) {
            return element.InnerHtml;
        }

        if (!definition.ValueKind.Equals("Text", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException(
                $"Unsupported DOM field value kind '{definition.ValueKind}'. Use Text or Html.",
                nameof(definition.ValueKind));
        }

        return NormalizeWhitespace(element.TextContent);
    }

    private static HtmlDomSelectorFieldCandidate[] DiscoverFields(IReadOnlyList<IElement> items, Uri? baseUri) {
        int sampleItemCount = Math.Min(items.Count, 20);
        Dictionary<string, FieldAccumulator> fields = new(StringComparer.Ordinal);
        for (int itemIndex = 0; itemIndex < sampleItemCount; itemIndex++) {
            IElement item = items[itemIndex];
            HashSet<string> seenInItem = new(StringComparer.Ordinal);
            foreach (IElement element in item.QuerySelectorAll("*")) {
                foreach (FieldObservation observation in CreateFieldObservations(item, element, baseUri)) {
                    string key = observation.Selector + "\n" + observation.Attribute;
                    if (!fields.TryGetValue(key, out FieldAccumulator? accumulator)) {
                        accumulator = new FieldAccumulator(observation.Selector, observation.Attribute, element);
                        fields.Add(key, accumulator);
                    }

                    accumulator.TotalMatchCount++;
                    if (seenInItem.Add(key)) {
                        accumulator.ItemMatchCount++;
                    } else {
                        accumulator.MultiplePerItem = true;
                    }

                    if (observation.Value.Length > 0 && accumulator.SampleValues.Count < 5
                        && !accumulator.SampleValues.Contains(observation.Value, StringComparer.Ordinal)) {
                        accumulator.SampleValues.Add(observation.Value);
                    }
                }
            }
        }

        List<HtmlDomSelectorFieldCandidate> candidates = new();
        foreach (FieldAccumulator field in fields.Values) {
            int coverage = (int)Math.Round(field.ItemMatchCount * 100d / sampleItemCount);
            if (coverage < 40 || field.SampleValues.Count == 0) {
                continue;
            }

            int score = coverage;
            if (field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase)) score += 30;
            if (field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
                && ContainsSemanticToken(field.Selector, "product", "detail", "overlay", "canonical")) score += 25;
            if (field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
                && ContainsSemanticToken(field.Selector, "brand", "manufacturer", "category")) score -= 10;
            if (field.Attribute.Equals("src", StringComparison.OrdinalIgnoreCase)
                || field.Attribute.Equals("data-src", StringComparison.OrdinalIgnoreCase)) score += 15;
            if (ContainsSemanticToken(field.Selector, "title", "name", "heading")) score += 25;
            if (ContainsSemanticToken(field.Selector, "price", "amount", "cost", "value")) score += 25;
            if (ContainsSemanticToken(field.Selector, "data-type='sell'", "data-type='buy'")) score += 25;
            if (ContainsSemanticToken(field.Selector, "fraction", "separator", "currency", "whole")) score -= 40;
            if (field.Element.Children.Length > 0) score -= 25;
            if (field.MultiplePerItem) score -= 20;
            if (field.SampleValues.All(static value => value.Length <= 1)) score -= 20;

            candidates.Add(new HtmlDomSelectorFieldCandidate {
                Name = BuildPropertyName(field),
                Selector = field.Selector,
                Attribute = field.Attribute,
                ItemMatchCount = field.ItemMatchCount,
                CoveragePercent = coverage,
                MultiplePerItem = field.MultiplePerItem,
                SampleValues = field.SampleValues.ToArray(),
                Score = score
            });
        }

        HtmlDomSelectorFieldCandidate[] ranked = candidates
            .OrderByDescending(static field => field.Score)
            .ThenByDescending(static field => field.CoveragePercent)
            .ThenBy(static field => field.Selector, StringComparer.Ordinal)
            .Take(10)
            .ToArray();
        EnsureUniquePropertyNames(ranked);
        return ranked;
    }

    private static IEnumerable<FieldObservation> CreateFieldObservations(
        IElement item,
        IElement element,
        Uri? baseUri) {
        bool isLink = element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase)
            && element.HasAttribute("href");
        string? mediaAttribute = MediaSourceAttributes.FirstOrDefault(attribute =>
            !string.IsNullOrWhiteSpace(element.GetAttribute(attribute)));
        bool isMedia = mediaAttribute != null
            && (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase)
                || element.LocalName.Equals("source", StringComparison.OrdinalIgnoreCase)
                || element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase));
        string text = NormalizeWhitespace(element.TextContent);
        bool semanticText = IsSemanticTextElement(element)
            && text.Length > 0
            && text.Length <= 240;

        if (!isLink && !isMedia && !semanticText) {
            yield break;
        }

        string selector = BuildRelativeSelector(item, element);
        if (selector.Length == 0) {
            yield break;
        }

        if (isLink) {
            string link = ResolveUrl(element.GetAttribute("href") ?? string.Empty, baseUri);
            if (link.Length > 0) {
                yield return new FieldObservation(selector, "href", link);
            }
        }

        if (isMedia) {
            string media = element.GetAttribute(mediaAttribute!) ?? string.Empty;
            if (SourceSetAttributes.Contains(mediaAttribute!, StringComparer.OrdinalIgnoreCase)) {
                media = HtmlImageCandidateParser.GetBestSourceSetSource(media);
            }

            media = ResolveUrl(media, baseUri);
            if (media.Length > 0) {
                yield return new FieldObservation(selector, mediaAttribute!, media);
            }
        }

        if (semanticText) {
            yield return new FieldObservation(selector, string.Empty, text);
        }
    }

    private static bool IsSemanticTextElement(IElement element) {
        if (new[] { "a", "h1", "h2", "h3", "h4", "h5", "h6", "p", "span", "strong", "em", "time", "data", "dd", "dt" }
            .Contains(element.LocalName, StringComparer.OrdinalIgnoreCase)) {
            return true;
        }

        return element.Children.Length == 0 && element.ClassList.Length > 0;
    }

    private static string BuildRelativeSelector(IElement item, IElement element) {
        string local = BuildLocalSelector(element, includeSemanticAttribute: true);
        if (local.Length == 0) {
            return string.Empty;
        }

        try {
            if (item.QuerySelectorAll(local).Length <= 1) {
                return local;
            }
        } catch {
            return string.Empty;
        }

        IElement? parent = element.ParentElement;
        int depth = 0;
        while (parent != null && !ReferenceEquals(parent, item) && depth < 3) {
            string parentSelector = BuildLocalSelector(parent, includeSemanticAttribute: true);
            if (parentSelector.Length > 0) {
                string scoped = parentSelector + " " + local;
                try {
                    if (item.QuerySelectorAll(scoped).Length <= 1) {
                        return scoped;
                    }
                } catch {
                    return local;
                }
            }

            parent = parent.ParentElement;
            depth++;
        }

        return BuildPositionalSelector(item, element) ?? local;
    }

    private static string? BuildPositionalSelector(IElement item, IElement element) {
        List<string> segments = new();
        for (IElement? current = element;
             current != null && !ReferenceEquals(current, item) && segments.Count < 4;
             current = current.ParentElement) {
            string segment = BuildLocalSelector(current, includeSemanticAttribute: true);
            IElement? parent = current.ParentElement;
            if (parent != null) {
                IElement[] sameTagSiblings = parent.Children
                    .Where(sibling => sibling.LocalName.Equals(current.LocalName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (sameTagSiblings.Length > 1) {
                    int position = Array.IndexOf(sameTagSiblings, current) + 1;
                    segment += $":nth-of-type({position})";
                }
            }

            segments.Insert(0, segment);
            string candidate = string.Join(" > ", segments);
            try {
                if (item.QuerySelectorAll(candidate).Length == 1) {
                    return candidate;
                }
            } catch {
                return null;
            }
        }

        return null;
    }

    private static string BuildLocalSelector(IElement element, bool includeSemanticAttribute) {
        StringBuilder builder = new(element.LocalName);
        int classCount = 0;
        foreach (string className in element.ClassList) {
            if (!SafeCssIdentifier.IsMatch(className) || IsUnstableClass(className)) {
                continue;
            }

            builder.Append('.').Append(className);
            classCount++;
            if (classCount == 4) {
                break;
            }
        }

        if (includeSemanticAttribute) {
            foreach (string attributeName in SemanticAttributes) {
                string? value = element.GetAttribute(attributeName);
                if (string.IsNullOrWhiteSpace(value) || value!.Length > 80 || value.Contains("'")) {
                    continue;
                }

                builder.Append('[').Append(attributeName).Append("='").Append(value).Append("']");
                break;
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> BuildCandidateSelectors(IElement element) {
        yield return element.LocalName;
        foreach (string className in element.ClassList) {
            if (SafeCssIdentifier.IsMatch(className) && !IsUnstableClass(className)) {
                yield return element.LocalName + "." + className;
            }
        }

        string complete = BuildLocalSelector(element, includeSemanticAttribute: false);
        if (!complete.Equals(element.LocalName, StringComparison.Ordinal)) {
            yield return complete;
        }
    }

    private static bool IsUnstableClass(string className) =>
        className.Equals("active", StringComparison.OrdinalIgnoreCase)
        || className.Equals("selected", StringComparison.OrdinalIgnoreCase)
        || className.Equals("current", StringComparison.OrdinalIgnoreCase)
        || className.Equals("first", StringComparison.OrdinalIgnoreCase)
        || className.Equals("last", StringComparison.OrdinalIgnoreCase)
        || className.Equals("odd", StringComparison.OrdinalIgnoreCase)
        || className.Equals("even", StringComparison.OrdinalIgnoreCase);

    private static int ScoreCollection(
        IReadOnlyList<IElement> items,
        IReadOnlyList<HtmlDomSelectorFieldCandidate> fields,
        string query,
        string candidateSelector) {
        int score = Math.Min(items.Count * 5, 35) + Math.Min(fields.Count * 8, 48);
        if (fields.Any(static field => field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase))) score += 15;
        if (fields.Any(static field => ContainsSemanticToken(field.Selector, "title", "name", "heading"))) score += 15;
        if (fields.Any(static field => ContainsSemanticToken(field.Selector, "price", "amount", "cost"))) score += 15;
        if (HasExactSelectorClass(candidateSelector, "product", "card", "item")) {
            score += 16;
        } else if (ContainsSemanticToken(candidateSelector, "product", "card", "item", "result", "listing")) {
            score += 8;
        }
        if (ContainsSemanticToken(candidateSelector, "grid", "column", "col-")) score -= 10;
        if (candidateSelector.Equals(items[0].LocalName, StringComparison.Ordinal)) score -= 5;
        if (query.Length > 0 && items.Any(item => ElementOrDescendantMatchesQuery(item, query))) score += 20;
        if (items.Any(IsInsideNavigation)) score -= 30;
        if (items.Count > 100) score -= 20;
        return score;
    }

    private static bool HasExactSelectorClass(string selector, params string[] classNames) =>
        selector.Split('.')
            .Skip(1)
            .Any(part => classNames.Contains(part, StringComparer.OrdinalIgnoreCase));

    private static string BuildCollectionReason(
        IReadOnlyList<IElement> items,
        IReadOnlyList<HtmlDomSelectorFieldCandidate> fields,
        string query) {
        List<string> reasons = new() {
            $"{items.Count} repeated elements",
            $"{fields.Count} likely fields"
        };
        int links = fields.Count(static field => field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase));
        if (links > 0) reasons.Add($"{links} link field(s)");
        if (query.Length > 0) reasons.Add($"matches query '{query}'");
        if (items.Any(IsInsideNavigation)) reasons.Add("inside navigation, ranked lower");
        return string.Join("; ", reasons) + ".";
    }

    private static bool IsInsideNavigation(IElement element) {
        for (IElement? current = element; current != null; current = current.ParentElement) {
            if (current.LocalName.Equals("nav", StringComparison.OrdinalIgnoreCase)
                || current.LocalName.Equals("header", StringComparison.OrdinalIgnoreCase)
                || current.GetAttribute("role")?.Equals("navigation", StringComparison.OrdinalIgnoreCase) == true) {
                return true;
            }
        }

        return false;
    }

    private static bool ElementOrDescendantMatchesQuery(IElement element, string query) {
        if (element.TextContent.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) {
            return true;
        }

        return new[] { element }
            .Concat(element.QuerySelectorAll("*"))
            .SelectMany(static current => current.Attributes)
            .Any(attribute =>
                attribute.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || attribute.Value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string BuildPropertyName(FieldAccumulator field) {
        if (field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase)) {
            if (ContainsSemanticToken(field.Selector, "product", "detail", "overlay", "canonical")) return "ProductLink";
            if (ContainsSemanticToken(field.Selector, "brand", "manufacturer")) return "BrandLink";
            if (ContainsSemanticToken(field.Selector, "image", "figure", "photo", "thumbnail")) return "ImageLink";
            return "Link";
        }

        if (field.Element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase)) {
            return "LinkText";
        }

        if (MediaSourceAttributes.Contains(field.Attribute, StringComparer.OrdinalIgnoreCase)) {
            return "Image";
        }

        string? dataType = field.Element.GetAttribute("data-type");
        if (!string.IsNullOrWhiteSpace(dataType)
            && ContainsSemanticToken(field.Selector, "price", "amount", "cost", "value")) {
            return ToPascalCase(dataType!) + "Price";
        }

        if (Regex.IsMatch(field.Element.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
            return "Title";
        }

        if (ContainsSemanticToken(field.Selector, "price", "amount", "cost")) {
            return "Price";
        }

        string semantic = field.Element.GetAttribute("itemprop")
            ?? field.Element.GetAttribute("name")
            ?? field.Element.ClassList.FirstOrDefault(className =>
                ContainsSemanticToken(className, "title", "name", "price", "amount", "date", "time", "stock", "value"))
            ?? field.Element.ClassList.FirstOrDefault()
            ?? field.Element.LocalName;
        string[] parts = PropertyTokenSplit.Split(semantic)
            .Where(static part => part.Length > 0)
            .Where(static part => !part.Equals("product", StringComparison.OrdinalIgnoreCase))
            .Where(static part => !part.Equals("card", StringComparison.OrdinalIgnoreCase))
            .Where(static part => !part.Equals("inner", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (parts.Length == 0) {
            return "Value";
        }

        return string.Concat(parts.Select(static part =>
            char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1) : string.Empty)));
    }

    private static string ToPascalCase(string value) {
        string[] parts = PropertyTokenSplit.Split(value ?? string.Empty)
            .Where(static part => part.Length > 0)
            .ToArray();
        return parts.Length == 0
            ? "Value"
            : string.Concat(parts.Select(static part =>
                char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1) : string.Empty)));
    }

    private static bool ContainsSemanticToken(string value, params string[] tokens) =>
        tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

    private static Uri? GetEffectiveBaseUri(IDocument document, Uri? baseUri) {
        string? baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(baseHref)) {
            return baseUri;
        }

        if (!Uri.TryCreate(baseHref, UriKind.RelativeOrAbsolute, out Uri? parsed)) {
            return baseUri;
        }

        if (parsed!.IsAbsoluteUri) {
            return parsed;
        }

        return baseUri == null ? null : new Uri(baseUri, parsed);
    }

    private static string ResolveUrl(string value, Uri? baseUri) {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 || baseUri == null) {
            return normalized;
        }

        return Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out Uri? parsed)
            ? (parsed!.IsAbsoluteUri ? parsed.ToString() : new Uri(baseUri, parsed).ToString())
            : normalized;
    }

    private static string NormalizeWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, @"\s+", " ").Trim();

    private sealed class FieldObservation {
        internal FieldObservation(string selector, string attribute, string value) {
            Selector = selector;
            Attribute = attribute;
            Value = value;
        }

        internal string Selector { get; }
        internal string Attribute { get; }
        internal string Value { get; }
    }

    private sealed class FieldAccumulator {
        internal FieldAccumulator(string selector, string attribute, IElement element) {
            Selector = selector;
            Attribute = attribute;
            Element = element;
        }

        internal string Selector { get; }
        internal string Attribute { get; }
        internal IElement Element { get; }
        internal int ItemMatchCount { get; set; }
        internal int TotalMatchCount { get; set; }
        internal bool MultiplePerItem { get; set; }
        internal List<string> SampleValues { get; } = new();
    }
}
