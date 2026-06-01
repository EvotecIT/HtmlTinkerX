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
    public static string ConvertToText(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        var result = NUglify.Uglify.HtmlToText(html);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"Convert-HTMLToText -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
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
        RemoveNoise(document);

        if (!string.IsNullOrWhiteSpace(preferredSelector)) {
            IElement? preferred = document.QuerySelector(preferredSelector!);
            if (preferred != null && CountWords(preferred.TextContent) > 0) {
                return BuildReadableTextResult(preferred, document, candidateCount: 1);
            }
        }

        IReadOnlyList<IElement> candidates = BuildReadableCandidates(document).ToArray();
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

        IElement selected = SelectReadableCandidate(candidates);
        return BuildReadableTextResult(selected, document, candidates.Count);
    }

    private static HtmlReadableTextResult BuildReadableTextResult(IElement selected, IDocument document, int candidateCount) {
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
            Score = ScoreReadableCandidate(selected),
            CandidateCount = candidateCount
        };
    }

    private static IElement SelectReadableCandidate(IReadOnlyList<IElement> candidates) {
        IElement? strongCandidate = candidates
            .Where(HasStrongReadableContainerSignal)
            .Select(element => new { Element = element, Score = ScoreReadableCandidate(element) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => CountWords(candidate.Element.TextContent))
            .FirstOrDefault()
            ?.Element;
        if (strongCandidate != null) {
            return strongCandidate;
        }

        return candidates
            .Select(element => new { Element = element, Score = ScoreReadableCandidate(element) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => CountWords(candidate.Element.TextContent))
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

    private static IEnumerable<IElement> BuildReadableCandidates(IDocument document) {
        List<IElement> candidates = document.QuerySelectorAll("main, article, [role='main'], section, div").ToList();
        if (candidates.Count == 0 && document.Body != null) {
            candidates.Add(document.Body);
        }

        return candidates
            .Where(static element => CountWords(element.TextContent) >= 12 || CountAttachmentSignals(element) > 0)
            .Distinct();
    }

    private static double ScoreReadableCandidate(IElement element) {
        string text = NormalizeWhitespace(element.TextContent);
        int wordCount = CountWords(text);
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(static anchor => anchor.TextContent)));
        int paragraphCount = element.QuerySelectorAll("p").Length;
        int headingCount = element.QuerySelectorAll("h1, h2, h3").Length;
        int tableCount = element.QuerySelectorAll("table").Length;
        int attachmentSignalCount = CountAttachmentSignals(element);
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
        score -= CountBoilerplateSignals(element) * 35;

        if (wordCount > 500 && linkCount > 30) {
            score -= 250;
        }

        return score;
    }

    private static int CountAttachmentSignals(IElement element) {
        string combined = string.Join(" ",
            element.TextContent,
            string.Join(" ", element.QuerySelectorAll("a[href]").Select(static anchor => anchor.GetAttribute("href"))));
        return Regex.Matches(combined, @"\b(attachment|attachments|download|downloads|file|files|pdf|docx|xlsx|pptx|zip|zalacznik|zalaczniki|załącznik|załączniki)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    }

    private static int CountBoilerplateSignals(IElement element) {
        string combined = string.Join(" ",
            element.Id,
            element.ClassName,
            element.GetAttribute("role"),
            element.GetAttribute("aria-label"),
            element.TextContent);
        return Regex.Matches(combined, @"\b(nav|navbar|menu|breadcrumb|breadcrumbs|footer|header|sidebar|search|cookie|cookies|social|share|pagination|strona główna|wyszukaj|hamburger|drukuj|metryczka)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
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

    private static void RemoveNoise(IParentNode container) {
        foreach (IElement element in container.QuerySelectorAll("script,style,noscript,svg,header,nav,footer,aside,[role='banner'],[role='navigation'],[role='contentinfo'],[role='search'],form[role='search'],.skip-link,.skip-link-screen-reader-text").ToArray()) {
            element.Remove();
        }

        foreach (IElement element in container.QuerySelectorAll("[hidden],[aria-hidden='true'],input[type='hidden'],[style]").ToArray()) {
            if (ShouldRemoveHiddenElement(element)) {
                element.Remove();
            }
        }

        foreach (IElement element in container.QuerySelectorAll("div,section,aside,dialog").ToArray()) {
            if (LooksLikeCookieOrConsentBanner(element)) {
                element.Remove();
            }
        }
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

    private static bool LooksLikeCookieOrConsentBanner(IElement element) {
        string combined = NormalizeWhitespace(string.Join(" ",
            element.Id,
            element.ClassName,
            element.GetAttribute("role"),
            element.GetAttribute("aria-label"),
            element.TextContent));
        if (string.IsNullOrWhiteSpace(combined)) {
            return false;
        }

        bool hasCookieSignal = Regex.IsMatch(combined, @"\b(cookie|cookies|consent|privacy|gdpr|rodo|plików cookies|pliki cookies)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!hasCookieSignal) {
            return false;
        }

        int wordCount = CountWords(combined);
        int linkCount = element.QuerySelectorAll("a[href]").Length;
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
