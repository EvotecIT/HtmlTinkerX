using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HtmlTinkerX;

/// <summary>
/// Utility helpers for working with HTML content.
/// </summary>
public static class HtmlParserToText {
    /// <summary>
    /// Converts HTML markup to plain text using NUglify.
    /// </summary>
    /// <param name="html">HTML string to convert.</param>
    /// <returns>Plain text extracted from the provided HTML.</returns>
    /// <example>
    /// <code>
    /// string text = HtmlParserToText.ConvertToText("<p>Hello</p>");
    /// </code>
    /// </example>
    public static string ConvertToText(string html) => ConvertToText(html, preserveStructure: false);

    /// <summary>
    /// Converts HTML markup to plain text, optionally retaining line breaks and preformatted indentation.
    /// </summary>
    /// <param name="html">HTML string to convert.</param>
    /// <param name="preserveStructure">Keep document line breaks and preformatted text when true; otherwise collapse whitespace as in the one-argument overload.</param>
    /// <returns>Plain text extracted from the provided HTML.</returns>
    /// <exception cref="ArgumentNullException">The HTML string is null.</exception>
    public static string ConvertToText(string html, bool preserveStructure) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        NUglify.Html.HtmlToTextOptions options = preserveStructure
            ? NUglify.Html.HtmlToTextOptions.KeepStructure : NUglify.Html.HtmlToTextOptions.None;
        var result = HasDocumentSectionTag(html)
            ? NUglify.Uglify.HtmlToText(html, options)
            : ConvertMarkupWithoutDocumentSectionsToText(html, options);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"Convert-HTMLToText -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
    }

    // Only start tags the HTML tokenizer reads as markup count. Tag-like text in comments, attribute values,
    // script, style and other raw-text elements is not a document section.
    private static bool HasDocumentSectionTag(string html) {
        bool found = false;
        var options = new AngleSharp.Html.Parser.HtmlParserOptions {
            OnToken = (token, _) => {
                if (!found && token.Type == AngleSharp.Html.Parser.HtmlTokenType.StartTag
                    && (string.Equals(token.Name, "html", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(token.Name, "head", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(token.Name, "body", StringComparison.OrdinalIgnoreCase))) {
                    found = true;
                }
            }
        };
        using IDocument document = new AngleSharp.Html.Parser.HtmlParser(options).ParseDocument(html);
        return found;
    }

    // NUglify's document parser opens an implicit head for leading script, style, title, meta or link
    // elements and never leaves it, so all following content is dropped from the text. Markup without
    // html, head or body tags is parsed as a fragment instead, with the same settings NUglify uses for
    // HtmlToText. A leading title is document metadata, not content, so it stays out of the text.
    private static NUglify.UglifyResult ConvertMarkupWithoutDocumentSectionsToText(string html, NUglify.Html.HtmlToTextOptions options) {
        var settings = new NUglify.Html.HtmlSettings {
            IsFragmentOnly = true,
            RemoveOptionalTags = false,
            RemoveEmptyAttributes = false,
            RemoveAttributeQuotes = false,
            RemoveScriptStyleTypeAttribute = false,
            ShortBooleanAttribute = false,
            MinifyJs = false,
            MinifyJsAttributes = false,
            MinifyCss = false,
            MinifyCssAttributes = false,
            DecodeEntityCharacters = true
        };
        var parser = new NUglify.Html.HtmlParser(html, null, settings);
        NUglify.Html.HtmlDocument document = parser.Parse();
        List<NUglify.UglifyError> errors = new(parser.Errors);
        foreach (NUglify.Html.HtmlNode node in document.Children.ToArray()) {
            if (node is NUglify.Html.HtmlElement element && string.Equals(element.Name, "title", StringComparison.OrdinalIgnoreCase)) {
                node.Remove();
            }
        }

        var minifier = new NUglify.Html.HtmlMinifier(document, settings);
        minifier.Minify();
        errors.AddRange(minifier.Errors);

        using var writer = new StringWriter();
        new NUglify.Html.HtmlWriterToText(writer, options).Write(document);
        return new NUglify.UglifyResult(writer.ToString(), errors);
    }

    /// <summary>
    /// Converts an HTML fragment to plain text, separating block elements and line breaks without inserting
    /// spaces inside inline markup. Scripts, styles and embedded content are omitted; no resources are loaded.
    /// </summary>
    /// <param name="html">The untrusted fragment of HTML body content.</param>
    /// <returns>Whitespace-normalized text from the fragment's body.</returns>
    /// <exception cref="ArgumentNullException">The fragment is null.</exception>
    /// <remarks>
    /// This method parses body content and uses HTML element semantics, not computed CSS layout. Contextual content
    /// such as table rows and cells needs its enclosing table. Its result is plain text, not sanitized HTML; encode it
    /// when rendering into an HTML document. <see cref="ConvertToText(string)"/> retains its existing behavior.
    /// </remarks>
    /// <example>
    /// <code>
    /// string text = HtmlParserToText.ConvertFragmentToText("&lt;ul&gt;&lt;li&gt;First&lt;/li&gt;&lt;li&gt;Second&lt;/li&gt;&lt;/ul&gt;");
    /// </code>
    /// </example>
    public static string ConvertFragmentToText(string html) => ConvertFragmentToText(html, preserveLineBreaks: false);

    /// <summary>
    /// Converts an HTML fragment to plain text, optionally keeping line and paragraph boundaries as newlines.
    /// </summary>
    /// <param name="html">The untrusted fragment of HTML body content.</param>
    /// <param name="preserveLineBreaks">
    /// When true, <c>&lt;br&gt;</c> and block elements such as paragraphs, list items, headings and table rows end a
    /// line, and newlines in text stay line breaks. When false, every boundary becomes one space, as in the
    /// one-argument overload.
    /// </param>
    /// <returns>Text from the fragment's body.</returns>
    /// <exception cref="ArgumentNullException">The fragment is null.</exception>
    /// <remarks>
    /// <para>
    /// With <paramref name="preserveLineBreaks"/> the result uses <c>\n</c> line endings. Inline elements join their
    /// text without a separator, so <c>Re&lt;b&gt;port&lt;/b&gt;</c> stays <c>Report</c>. Spaces and tabs collapse
    /// to one space, lines are trimmed, and table cells in a row are separated by a space. Each <c>&lt;br&gt;</c>
    /// adds a line break and a blank line needs two; longer runs of breaks collapse to one blank line, and leading
    /// and trailing blank lines are removed.
    /// </para>
    /// <para>
    /// Newlines in text separate lines only where no markup boundary does: the source formatting between
    /// <c>&lt;/p&gt;</c> and <c>&lt;p&gt;</c>, or after <c>&lt;br&gt;</c>, does not add blank lines. Preformatted
    /// text keeps its spacing and indentation. Scripts, styles, templates, noscript and embedded content are
    /// omitted, and the text after them is kept. Entities are decoded and the result is not HTML-encoded.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// string text = HtmlParserToText.ConvertFragmentToText("&lt;p&gt;Re&lt;b&gt;port&lt;/b&gt;&lt;/p&gt;&lt;p&gt;Line 1&lt;br&gt;Line 2&lt;/p&gt;", preserveLineBreaks: true);
    /// // "Report\nLine 1\nLine 2"
    /// </code>
    /// </example>
    public static string ConvertFragmentToText(string html, bool preserveLineBreaks) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        foreach (IElement element in document.QuerySelectorAll(NonTextContentSelector)) {
            element.Remove();
        }

        if (preserveLineBreaks) {
            if (document.Body == null) {
                return string.Empty;
            }

            var writer = new LineTextWriter();
            writer.WriteChildren(document.Body);
            return writer.ToString();
        }

        // Block boundaries are whitespace in text. Inline elements keep the exact adjacency of their text nodes.
        foreach (IElement element in document.QuerySelectorAll(BlockBoundarySelector)) {
            element.Parent?.InsertBefore(document.CreateTextNode(" "), element);
            element.Parent?.InsertBefore(document.CreateTextNode(" "), element.NextSibling);
        }

        return NormalizeWhitespace(document.Body?.TextContent);
    }

    private const string NonTextContentSelector = "script,style,template,noscript,iframe,object,embed,svg,math,audio,video,canvas,applet";

    private const string BlockBoundarySelector =
        "address,article,aside,blockquote,br,caption,center,dd,details,dialog,dir,div,dl,dt,fieldset,figcaption,figure,footer,form," +
        "h1,h2,h3,h4,h5,h6,header,hgroup,hr,legend,li,listing,main,menu,nav,ol,optgroup,option,p,plaintext,pre,search,section," +
        "summary,table,tbody,td,tfoot,th,thead,tr,ul,xmp";

    private static readonly HashSet<string> LineBlockElements = new(StringComparer.OrdinalIgnoreCase) {
        "address", "article", "aside", "blockquote", "caption", "center", "dd", "details", "dialog", "dir", "div", "dl", "dt",
        "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hgroup", "hr",
        "legend", "li", "listing", "main", "menu", "nav", "ol", "optgroup", "option", "p", "plaintext", "pre", "search",
        "section", "summary", "table", "tbody", "tfoot", "thead", "tr", "ul", "xmp"
    };

    private static readonly HashSet<string> PreformattedElements = new(StringComparer.OrdinalIgnoreCase) {
        "pre", "listing", "xmp", "plaintext", "textarea"
    };

    /// <summary>
    /// Writes text with line boundaries. Breaks requested by markup (block edges and br) take precedence over newlines
    /// in the whitespace between the same two pieces of text, which are treated as source formatting.
    /// </summary>
    private sealed class LineTextWriter {
        private readonly StringBuilder _builder = new();
        private readonly StringBuilder _pendingIndent = new();
        private bool _hasText;
        private bool _pendingSpace;
        private int _markupBreaks;
        private int _textBreaks;

        public void WriteChildren(INode parent) {
            foreach (INode child in parent.ChildNodes) {
                WriteNode(child, preformatted: false);
            }
        }

        private void WriteNode(INode node, bool preformatted) {
            if (node is IText text) {
                WriteText(text.Data, preformatted);
                return;
            }

            if (node is not IElement element) {
                return;
            }

            string name = element.LocalName;
            if (string.Equals(name, "br", StringComparison.OrdinalIgnoreCase)) {
                _markupBreaks++;
                _pendingIndent.Length = 0;
                return;
            }

            bool block = LineBlockElements.Contains(name);
            bool cell = string.Equals(name, "td", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "th", StringComparison.OrdinalIgnoreCase);
            bool childPreformatted = preformatted || PreformattedElements.Contains(name);
            if (block) {
                BlockBoundary();
            } else if (cell) {
                _pendingSpace = true;
            }

            foreach (INode child in element.ChildNodes) {
                WriteNode(child, childPreformatted);
            }

            if (block) {
                BlockBoundary();
            } else if (cell) {
                _pendingSpace = true;
            }
        }

        private void BlockBoundary() {
            _markupBreaks = Math.Max(_markupBreaks, 1);
            _pendingIndent.Length = 0;
        }

        private void WriteText(string data, bool preformatted) {
            for (int i = 0; i < data.Length; i++) {
                char c = data[i];
                if (c == '\r' || c == '\n') {
                    if (c == '\r' && i + 1 < data.Length && data[i + 1] == '\n') {
                        i++;
                    }
                    _textBreaks++;
                    _pendingSpace = false;
                    _pendingIndent.Length = 0;
                } else if (!preformatted && (char.IsWhiteSpace(c) || c == '\u00A0')) {
                    _pendingSpace = true;
                } else if (preformatted && (c == ' ' || c == '\t') && (!_hasText || _markupBreaks > 0 || _textBreaks > 0)) {
                    // Indentation at the start of a line is kept only when text follows on that line, so
                    // whitespace-only lines stay blank lines and count toward the blank-line limit.
                    _pendingIndent.Append(c);
                } else {
                    Append(c);
                }
            }
        }

        private void Append(char c) {
            if (_hasText) {
                int breaks = Math.Min(_markupBreaks > 0 ? _markupBreaks : _textBreaks, 2);
                if (breaks > 0) {
                    TrimTrailingSpaces();
                    _builder.Append('\n', breaks);
                } else if (_pendingSpace) {
                    _builder.Append(' ');
                }
            }

            _hasText = true;
            _pendingSpace = false;
            _markupBreaks = 0;
            _textBreaks = 0;
            _builder.Append(_pendingIndent);
            _pendingIndent.Length = 0;
            _builder.Append(c);
        }

        private void TrimTrailingSpaces() {
            int length = _builder.Length;
            while (length > 0 && (_builder[length - 1] == ' ' || _builder[length - 1] == '\t')) {
                length--;
            }
            _builder.Length = length;
        }

        public override string ToString() {
            TrimTrailingSpaces();
            return _builder.ToString();
        }
    }

    /// <summary>
    /// Extracts plain text from the most readable article-like region of an HTML document.
    /// </summary>
    /// <param name="html">HTML string to inspect.</param>
    /// <param name="preferredSelector">Optional CSS selector for a known content container.</param>
    /// <returns>Readable text extraction result.</returns>
    public static HtmlReadableTextResult ExtractReadableText(string html, string? preferredSelector = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        HtmlReadableTextAnalysis analysis = RemoveNoise(document);

        if (!string.IsNullOrWhiteSpace(preferredSelector)) {
            IElement? preferred = document.QuerySelector(preferredSelector!);
            if (preferred != null && analysis.Get(preferred).Words.Count > 0) {
                return BuildReadableTextResult(preferred, document, candidateCount: 1, analysis);
            }
        }

        IReadOnlyList<IElement> candidates = BuildReadableCandidates(document, analysis).ToArray();
        if (candidates.Count == 0) {
            string fallbackText = NormalizeWhitespace(ConvertToText(document.DocumentElement?.OuterHtml ?? html));
            string? fallbackTitle = FindDocumentTitle(document);
            string? fallbackDescription = FindDocumentDescription(document);
            if (IsWeakFallbackText(fallbackText, document)) {
                fallbackText = fallbackDescription ?? fallbackTitle ?? fallbackText;
            }

            return new HtmlReadableTextResult {
                Text = fallbackText,
                Title = fallbackTitle,
                CandidateCount = 0
            };
        }

        IElement selected = SelectReadableCandidate(candidates, analysis);
        return BuildReadableTextResult(selected, document, candidates.Count, analysis);
    }

    private static HtmlReadableTextResult BuildReadableTextResult(IElement selected, IDocument document, int candidateCount, HtmlReadableTextAnalysis analysis) {
        string selectedHtml = selected.OuterHtml;
        string text = NormalizeWhitespace(ConvertToText(selectedHtml));
        string domText = NormalizeWhitespace(selected.TextContent);
        if (string.IsNullOrWhiteSpace(text) || CountWords(text) < Math.Min(8, CountWords(domText))) {
            text = domText;
        }

        string? title = FindReadableTitle(selected, document);

        return new HtmlReadableTextResult {
            Text = text,
            Title = title,
            SelectorHint = BuildSelectorHint(selected),
            Score = ScoreReadableCandidate(selected, analysis),
            CandidateCount = candidateCount
        };
    }

    private static IElement SelectReadableCandidate(IReadOnlyList<IElement> candidates, HtmlReadableTextAnalysis analysis) {
        IElement? strongCandidate = candidates
            .Where(HasStrongReadableContainerSignal)
            .Select(element => new { Element = element, Score = ScoreReadableCandidate(element, analysis) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => analysis.Get(candidate.Element).Words.Count)
            .FirstOrDefault()
            ?.Element;
        if (strongCandidate != null) {
            return strongCandidate;
        }

        return candidates
            .Select(element => new { Element = element, Score = ScoreReadableCandidate(element, analysis) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => analysis.Get(candidate.Element).Words.Count)
            .First()
            .Element;
    }

    /// <summary>
    /// Converts HTML markup to readable plain text by selecting the most article-like region first.
    /// </summary>
    /// <param name="html">HTML string to inspect.</param>
    /// <param name="preferredSelector">Optional CSS selector for a known content container.</param>
    /// <returns>Readable plain text.</returns>
    public static string ConvertToReadableText(string html, string? preferredSelector = null) => ExtractReadableText(html, preferredSelector).Text;

    /// <summary>
    /// Converts HTML markup from a file to plain text using NUglify.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <returns>Plain text extracted from the provided HTML file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string ConvertFileToText(string filePath) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return ConvertToText(html);
    }

    private static IEnumerable<IElement> BuildReadableCandidates(IDocument document, HtmlReadableTextAnalysis analysis) {
        List<IElement> candidates = document.QuerySelectorAll("main, article, [role='main'], section, div").ToList();
        if (candidates.Count == 0 && document.Body != null) {
            candidates.Add(document.Body);
        }

        return candidates
            .Where(element => analysis.Get(element).Words.Count >= 12 || analysis.AttachmentCount(element) > 0)
            .Distinct();
    }

    private static double ScoreReadableCandidate(IElement element, HtmlReadableTextAnalysis analysis) {
        HtmlReadableTextAnalysis.Metrics metrics = analysis.Get(element);
        int wordCount = metrics.Words.Count;
        int linkCount = metrics.Links - (metrics.OwnLink ? 1 : 0);
        int linkWordCount = metrics.LinkWords - (metrics.OwnLink ? wordCount : 0);
        int paragraphCount = metrics.Paragraphs - (element.LocalName == "p" ? 1 : 0);
        int headingCount = metrics.Headings - (element.LocalName is "h1" or "h2" or "h3" ? 1 : 0);
        int tableCount = metrics.Tables - (element.LocalName == "table" ? 1 : 0);
        int attachmentSignalCount = analysis.AttachmentCount(element);
        double linkDensity = (double)linkWordCount / Math.Max(1, wordCount);

        double score = Math.Min(wordCount, 900);
        score += paragraphCount * 18;
        score += headingCount * 30;
        score += tableCount * 10;
        score += attachmentSignalCount * 45;

        string tagName = element.TagName.ToLowerInvariant();
        if (tagName == "article") {
            score += 60;
        } else if (tagName == "main") {
            score += 40;
        } else if (tagName == "section") {
            score += 15;
        }

        if (HasReadableContainerSignal(element)) {
            score += 500;
        }

        score -= linkCount * 12;
        score -= linkDensity * 220;
        score -= analysis.BoilerplateCount(element) * 35;

        if (wordCount > 500 && linkCount > 30) {
            score -= 250;
        }

        return score;
    }

    private static bool HasReadableContainerSignal(IElement element) {
        string combined = string.Join(" ",
            element.Id,
            element.ClassName,
            element.GetAttribute("role"),
            element.GetAttribute("itemprop"),
            element.GetAttribute("data-testid"));
        return Regex.IsMatch(combined, @"\b(article|content|main|notice|post|entry|document)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasStrongReadableContainerSignal(IElement element) {
        string tagName = element.TagName.ToLowerInvariant();
        if (tagName == "article") {
            return true;
        }

        string combined = string.Join(" ",
            element.Id,
            element.ClassName,
            element.GetAttribute("role"),
            element.GetAttribute("itemprop"),
            element.GetAttribute("data-testid")).ToLowerInvariant();
        return combined.Contains("article", StringComparison.Ordinal)
            && (combined.Contains("content", StringComparison.Ordinal)
                || combined.Contains("body", StringComparison.Ordinal)
                || combined.Contains("main", StringComparison.Ordinal));
    }

    private static HtmlReadableTextAnalysis RemoveNoise(IDocument container) {
        foreach (IElement element in container.QuerySelectorAll("script,style,noscript,svg,header,nav,footer,aside,[role='banner'],[role='navigation'],[role='contentinfo'],[role='search'],form[role='search'],.skip-link,.skip-link-screen-reader-text").ToArray()) {
            element.Remove();
        }

        foreach (IElement element in container.QuerySelectorAll("[hidden],[aria-hidden='true'],input[type='hidden'],[style]").ToArray()) {
            if (ShouldRemoveHiddenElement(element)) {
                element.Remove();
            }
        }

        var analysis = new HtmlReadableTextAnalysis(container);
        bool removedConsentElement = false;
        foreach (IElement element in container.QuerySelectorAll("div,section,aside,dialog").ToArray()) {
            if (LooksLikeCookieOrConsentBanner(element, analysis)) {
                element.Remove();
                removedConsentElement = true;
            }
        }
        return removedConsentElement ? new HtmlReadableTextAnalysis(container) : analysis;
    }

    private static bool ShouldRemoveHiddenElement(IElement element) {
        if (element.HasAttribute("hidden")) {
            return true;
        }

        if (string.Equals(element.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (element.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        string? style = element.GetAttribute("style");
        if (string.IsNullOrWhiteSpace(style)) {
            return false;
        }

        string normalizedStyle = Regex.Replace(style, @"\s+", string.Empty).ToLowerInvariant();
        return normalizedStyle.Contains("display:none")
            || normalizedStyle.Contains("visibility:hidden")
            || normalizedStyle.Contains("content-visibility:hidden");
    }

    private static bool LooksLikeCookieOrConsentBanner(IElement element, HtmlReadableTextAnalysis analysis) {
        if (!analysis.HasCookieSignal(element)) {
            return false;
        }

        int wordCount = analysis.CookieWordCount(element);
        int linkCount = analysis.Get(element).Links - (analysis.Get(element).OwnLink ? 1 : 0);
        return wordCount <= 120 || linkCount >= 1;
    }

    private static string? FindReadableTitle(IElement selected, IDocument document) {
        string? heading = selected.QuerySelector("h1, h2, h3")?.TextContent;
        string normalizedHeading = NormalizeWhitespace(heading);
        if (!string.IsNullOrWhiteSpace(normalizedHeading)) {
            return normalizedHeading;
        }

        return FindDocumentTitle(document);
    }

    private static string? FindDocumentTitle(IDocument document) =>
        FirstNonEmptyMetaContent(document, ("property", "og:title"), ("name", "twitter:title"), ("name", "title"))
        ?? NullIfWhiteSpace(NormalizeWhitespace(document.Title));

    private static string? FindDocumentDescription(IDocument document) =>
        FirstNonEmptyMetaContent(document, ("property", "og:description"), ("name", "description"), ("name", "twitter:description"));

    private static string? FirstNonEmptyMetaContent(IDocument document, params (string Attribute, string Value)[] keys) {
        foreach ((string attribute, string value) in keys) {
            foreach (IElement meta in document.QuerySelectorAll("meta")) {
                if (string.Equals(meta.GetAttribute(attribute), value, StringComparison.OrdinalIgnoreCase)) {
                    string normalized = NormalizeWhitespace(meta.GetAttribute("content"));
                    if (!string.IsNullOrWhiteSpace(normalized)) {
                        return normalized;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsWeakFallbackText(string text, IDocument document) {
        if (string.IsNullOrWhiteSpace(text)) {
            return true;
        }

        string normalizedTitle = NormalizeWhitespace(document.Title);
        return CountWords(text) <= 6
            || (!string.IsNullOrWhiteSpace(normalizedTitle) && string.Equals(text, normalizedTitle, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NullIfWhiteSpace(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static string? BuildSelectorHint(IElement element) {
        StringBuilder builder = new(element.TagName.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            builder.Append('#').Append(element.Id);
            return builder.ToString();
        }

        foreach (string className in element.ClassList.Where(static className => !string.IsNullOrWhiteSpace(className)).Take(3)) {
            builder.Append('.').Append(className);
        }

        return builder.ToString();
    }

    private static int CountWords(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return 0;
        }

        return Regex.Matches(text, @"[\p{L}\p{N}][\p{L}\p{N}'’-]*", RegexOptions.CultureInvariant).Count;
    }

    private static string NormalizeWhitespace(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : Regex.Replace(text, @"\s+", " ").Trim();
}
