using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    private static readonly TimeSpan TextPatternTimeout = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> NonTextTags = new(StringComparer.Ordinal) { "script", "style", "template" };

    /// <summary>
    /// Selects strings with a CSS selector that ends in <c>::text</c> or <c>::attr(name)</c>.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <param name="selector">CSS selector ending in <c>::text</c> (the element's own text) or <c>::attr(name)</c> (an attribute value),
    /// for example <c>a.next::attr(href)</c> or <c>li *::text</c>.</param>
    /// <param name="baseUri">Optional page URL used to resolve URL attributes such as <c>href</c> and <c>src</c>. A <c>&lt;base href&gt;</c> in the page takes precedence.</param>
    /// <param name="first">Return only the first value.</param>
    /// <returns>Values in document order. Elements without own text, or without the attribute, are skipped.</returns>
    public static IReadOnlyList<string> SelectValues(string html, string selector, Uri? baseUri = null, bool first = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        return SelectValues(HtmlParser.ParseWithAngleSharp(html), selector, baseUri, first);
    }

    /// <summary>
    /// Selects strings below a parsed document or element with a CSS selector that ends in <c>::text</c> or <c>::attr(name)</c>.
    /// </summary>
    /// <param name="root">Document or element to search.</param>
    /// <param name="selector">CSS selector ending in <c>::text</c> or <c>::attr(name)</c>.</param>
    /// <param name="baseUri">Optional page URL used to resolve URL attributes. A <c>&lt;base href&gt;</c> in the page takes precedence.</param>
    /// <param name="first">Return only the first value.</param>
    /// <returns>Values in document order. Elements without own text, or without the attribute, are skipped.</returns>
    public static IReadOnlyList<string> SelectValues(IParentNode root, string selector, Uri? baseUri = null, bool first = false) {
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        HtmlValueSelector valueSelector = ParseValueSelector(selector)
            ?? throw new ArgumentException(
                $"CSS selector '{selector}' must end with ::text or ::attr(name) to select values. Use SelectElements to select elements.",
                nameof(selector));
        if (valueSelector.ElementSelector.Length == 0) {
            throw new ArgumentException(
                $"CSS selector '{selector}' needs an element selector before {valueSelector.PseudoElement}, for example 'a{valueSelector.PseudoElement}'.",
                nameof(selector));
        }

        return ReadValues(QueryElements(root, valueSelector.ElementSelector, nameof(selector)), valueSelector, ResolveBaseUri(root, baseUri), first);
    }

    /// <summary>
    /// Finds elements by their own text, that is the text directly inside the element and not inside its child elements.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <param name="text">Text to find, or a regular expression when <paramref name="match"/> is <see cref="HtmlTextMatch.Regex"/>.</param>
    /// <param name="match">How the own text is compared.</param>
    /// <param name="selector">Optional CSS selector that limits the candidates, for example <c>a</c> or <c>.menu li</c>.</param>
    /// <param name="caseSensitive">Compare case-sensitively. Matching ignores case by default.</param>
    /// <returns>Matching elements in document order.</returns>
    public static IReadOnlyList<IElement> FindByText(
        string html, string text, HtmlTextMatch match = HtmlTextMatch.Exact, string? selector = null, bool caseSensitive = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        return FindByText(HtmlParser.ParseWithAngleSharp(html), text, match, selector, caseSensitive);
    }

    /// <summary>
    /// Finds elements below a parsed document or element by their own text.
    /// </summary>
    /// <param name="root">Document or element to search.</param>
    /// <param name="text">Text to find, or a regular expression when <paramref name="match"/> is <see cref="HtmlTextMatch.Regex"/>.</param>
    /// <param name="match">How the own text is compared.</param>
    /// <param name="selector">Optional CSS selector that limits the candidates.</param>
    /// <param name="caseSensitive">Compare case-sensitively. Matching ignores case by default.</param>
    /// <returns>Matching elements in document order.</returns>
    public static IReadOnlyList<IElement> FindByText(
        IParentNode root, string text, HtmlTextMatch match = HtmlTextMatch.Exact, string? selector = null, bool caseSensitive = false) {
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        if (string.IsNullOrWhiteSpace(text)) {
            throw new ArgumentException("Search text cannot be empty.", nameof(text));
        }

        Func<string, bool> isMatch = CreateTextMatcher(text, match, caseSensitive);
        IEnumerable<IElement> candidates = string.IsNullOrWhiteSpace(selector)
            ? root.QuerySelectorAll("*")
            : QueryElements(root, selector!, nameof(selector));
        return candidates
            .Where(element => !NonTextTags.Contains(element.LocalName))
            .Where(element => {
                string ownText = GetOwnText(element);
                return ownText.Length > 0 && isMatch(ownText);
            })
            .ToArray();
    }

    internal static HtmlValueSelector? ParseValueSelector(string selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("CSS selector cannot be empty.", nameof(selector));
        }

        char quote = '\0';
        int depth = 0;
        bool hasSelectorList = false;
        for (int index = 0; index < selector.Length; index++) {
            char current = selector[index];
            if (quote != '\0') {
                if (current == '\\') {
                    index++;
                } else if (current == quote) {
                    quote = '\0';
                }

                continue;
            }

            switch (current) {
                case '\\':
                    index++;
                    continue;
                case '"':
                case '\'':
                    quote = current;
                    continue;
                case '[':
                case '(':
                    depth++;
                    continue;
                case ']':
                case ')':
                    depth--;
                    continue;
                case ',' when depth == 0:
                    hasSelectorList = true;
                    continue;
            }

            if (depth != 0 || current != ':' || index + 1 >= selector.Length || selector[index + 1] != ':') {
                continue;
            }

            HtmlValueSelector? parsed = TryReadValuePseudoElement(selector, index, out int end);
            if (parsed == null) {
                index++;
                continue;
            }

            if (end < selector.Length && selector.Substring(end).Trim().Length > 0) {
                throw new ArgumentException(
                    $"CSS selector '{selector}' uses {parsed.PseudoElement} before the end. ::text and ::attr(name) must be the last part of the selector.",
                    nameof(selector));
            }

            if (hasSelectorList) {
                throw new ArgumentException(
                    $"CSS selector '{selector}' combines a selector list with {parsed.PseudoElement}. Select each value separately.",
                    nameof(selector));
            }

            return parsed;
        }

        return null;
    }

    // A field selector such as "a::attr(href)" chooses its own value; an empty element part reads the item itself.
    private static HtmlValueSelector? ParseFieldValueSelector(HtmlDomFieldDefinition definition, string propertyName) {
        if (string.IsNullOrWhiteSpace(definition.Selector)) {
            return null;
        }

        HtmlValueSelector? valueSelector;
        try {
            valueSelector = ParseValueSelector(definition.Selector);
        } catch (ArgumentException exception) {
            throw new ArgumentException($"Property '{propertyName}': {exception.Message}", nameof(definition.Selector), exception);
        }

        if (valueSelector != null && (!string.IsNullOrWhiteSpace(definition.Attribute)
            || !string.Equals(definition.ValueKind, "Text", StringComparison.OrdinalIgnoreCase))) {
            throw new ArgumentException(
                $"Property '{propertyName}' selector '{definition.Selector}' already chooses its value with {valueSelector.PseudoElement}. Remove Attribute and ValueKind.",
                nameof(definition.Selector));
        }

        return valueSelector;
    }

    internal static IReadOnlyList<string> ReadValues(IEnumerable<IElement> elements, HtmlValueSelector valueSelector, Uri? baseUri, bool first) {
        List<string> values = new();
        foreach (IElement element in elements) {
            string? value = valueSelector.Read(element, resolveUrl: false, baseUri);
            if (value == null || (valueSelector.AttributeName == null && value.Length == 0)) {
                continue;
            }

            values.Add(value);
            if (first) {
                break;
            }
        }

        return values;
    }

    internal static Uri? ResolveBaseUri(IParentNode root, Uri? baseUri) {
        IDocument? document = root as IDocument ?? (root as INode)?.Owner;
        return document == null ? baseUri : GetEffectiveBaseUri(document, baseUri);
    }

    internal static string GetOwnText(IElement element) {
        StringBuilder text = new();
        foreach (INode child in element.ChildNodes) {
            if (child.NodeType == NodeType.Text) {
                text.Append(child.TextContent).Append(' ');
            }
        }

        return NormalizeWhitespace(text.ToString());
    }

    private static string? ReadAttributeValue(IElement element, string attributeName, bool resolveUrl, Uri? baseUri) {
        string? value = element.GetAttribute(attributeName);
        if (value == null) {
            return null;
        }

        if (SourceSetAttributes.Contains(attributeName, StringComparer.OrdinalIgnoreCase)) {
            value = HtmlImageCandidateParser.GetBestSourceSetSource(value);
        }

        bool shouldResolve = resolveUrl || UriAttributes.Contains(attributeName, StringComparer.OrdinalIgnoreCase);
        return shouldResolve ? ResolveUrl(value, baseUri) : value;
    }

    internal static IHtmlCollection<IElement> QueryElements(IParentNode root, string selector, string parameterName) {
        try {
            return root.QuerySelectorAll(selector);
        } catch (Exception exception) {
            throw new ArgumentException($"CSS selector '{selector}' is invalid: {exception.Message}", parameterName, exception);
        }
    }

    private static Func<string, bool> CreateTextMatcher(string text, HtmlTextMatch match, bool caseSensitive) {
        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        switch (match) {
            case HtmlTextMatch.Exact:
                string expected = NormalizeWhitespace(text);
                return ownText => string.Equals(ownText, expected, comparison);
            case HtmlTextMatch.Contains:
                string fragment = NormalizeWhitespace(text);
                return ownText => ownText.IndexOf(fragment, comparison) >= 0;
            case HtmlTextMatch.Regex:
                RegexOptions options = RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                Regex pattern;
                try {
                    pattern = new Regex(text, options, TextPatternTimeout);
                } catch (ArgumentException exception) {
                    throw new ArgumentException($"Text pattern '{text}' is not a valid regular expression: {exception.Message}", nameof(text), exception);
                }

                return ownText => pattern.IsMatch(ownText);
            default:
                throw new ArgumentOutOfRangeException(nameof(match), match, "Unsupported text match mode.");
        }
    }

    private static HtmlValueSelector? TryReadValuePseudoElement(string selector, int start, out int end) {
        int nameStart = start + 2;
        int nameEnd = nameStart;
        while (nameEnd < selector.Length && (char.IsLetterOrDigit(selector[nameEnd]) || selector[nameEnd] == '-')) {
            nameEnd++;
        }

        string name = selector.Substring(nameStart, nameEnd - nameStart);
        end = nameEnd;
        string elementSelector = ToElementSelector(selector.Substring(0, start));
        if (name.Equals("text", StringComparison.OrdinalIgnoreCase)) {
            return new HtmlValueSelector(elementSelector, null);
        }

        if (!name.Equals("attr", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        int close = nameEnd < selector.Length && selector[nameEnd] == '(' ? selector.IndexOf(')', nameEnd) : -1;
        string attributeName = close < 0 ? string.Empty : selector.Substring(nameEnd + 1, close - nameEnd - 1).Trim().Trim('"', '\'');
        if (attributeName.Length == 0 || attributeName.Any(char.IsWhiteSpace)) {
            throw new ArgumentException(
                $"CSS selector '{selector}' must name one attribute in ::attr(name), for example a::attr(href).",
                nameof(selector));
        }

        end = close + 1;
        return new HtmlValueSelector(elementSelector, attributeName);
    }

    // "li *::text", "li ::text" and "li > ::text" all mean the descendants of li, as in Scrapy.
    private static string ToElementSelector(string prefix) {
        string trimmed = prefix.TrimEnd();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        char last = trimmed[trimmed.Length - 1];
        return trimmed.Length < prefix.Length || last == '>' || last == '+' || last == '~'
            ? trimmed + " *"
            : trimmed;
    }

    internal sealed class HtmlValueSelector {
        internal HtmlValueSelector(string elementSelector, string? attributeName) {
            ElementSelector = elementSelector;
            AttributeName = attributeName;
        }

        /// <summary>Selector for the elements whose values are read; empty means the current element.</summary>
        internal string ElementSelector { get; }

        /// <summary>Attribute to read, or <see langword="null"/> for the element's own text.</summary>
        internal string? AttributeName { get; }

        internal string PseudoElement => AttributeName == null ? "::text" : $"::attr({AttributeName})";

        internal string? Read(IElement element, bool resolveUrl, Uri? baseUri) =>
            AttributeName == null ? GetOwnText(element) : ReadAttributeValue(element, AttributeName, resolveUrl, baseUri);
    }
}
