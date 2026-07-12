using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static string? ExtractTitle(string html) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return document.Title;
    }

    private static string? ExtractCanonicalUrl(string html, Uri baseUri, HtmlCrawlOptions options) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri effectiveBaseUri = GetDocumentBaseUri(document, baseUri);
        IElement? canonical = document.QuerySelector("link[rel='canonical'][href], link[rel~='canonical'][href]");
        string? href = canonical?.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) {
            return null;
        }

        if (!Uri.TryCreate(effectiveBaseUri, href, out Uri? resolved)) {
            return null;
        }

        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps) {
            return null;
        }

        return NormalizeUrl(resolved, options);
    }

    private static ContentSelectionResult SelectContent(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return new ContentSelectionResult {
                ModeUsed = options.ContentMode,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.None,
                Reason = "No HTML was available for content selection.",
                Html = html
            };
        }

        if (options.ContentMode == HtmlCrawlContentMode.Raw && string.IsNullOrWhiteSpace(options.Selector)) {
            return new ContentSelectionResult {
                ModeUsed = HtmlCrawlContentMode.Raw,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.RawDocument,
                Reason = "Raw mode kept the full fetched document because no selector was configured.",
                Html = html
            };
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        switch (options.ContentMode) {
            case HtmlCrawlContentMode.Raw:
                IElement? rawElement = document.QuerySelector(options.Selector!);
                if (rawElement != null) {
                    return BuildContentSelectionResult(
                        HtmlCrawlContentMode.Raw,
                        HtmlCrawlContentSelectionReasonCode.RawSelector,
                        $"Raw mode selected the exact configured selector '{options.Selector}'.",
                        rawElement);
                }

                return new ContentSelectionResult {
                    ModeUsed = HtmlCrawlContentMode.Raw,
                    ReasonCode = HtmlCrawlContentSelectionReasonCode.RawSelectorMiss,
                    Reason = $"Raw mode produced no stored content because selector '{options.Selector}' was not found.",
                    Html = string.Empty
                };
            case HtmlCrawlContentMode.Reader:
                return SelectReaderContent(document, html, options);
            default:
                if (!string.IsNullOrWhiteSpace(options.Selector)) {
                    IElement? selected = document.QuerySelector(options.Selector!);
                    if (selected != null) {
                        return BuildContentSelectionResult(
                            HtmlCrawlContentMode.Focused,
                            HtmlCrawlContentSelectionReasonCode.FocusedSelector,
                            $"Focused mode selected the configured selector '{options.Selector}'.",
                            selected);
                    }
                }

                if ((string.IsNullOrWhiteSpace(options.Selector) || LooksLikeSemanticContentSelector(options.Selector))
                    && TrySelectPreferredContentElement(document, out IElement? fallback)) {
                    return BuildContentSelectionResult(
                        HtmlCrawlContentMode.Focused,
                        HtmlCrawlContentSelectionReasonCode.FocusedSemanticFallback,
                        $"Focused mode fell back to semantic content element {DescribeElement(fallback!)}.",
                        fallback!);
                }

                return new ContentSelectionResult {
                    ModeUsed = HtmlCrawlContentMode.Focused,
                    ReasonCode = HtmlCrawlContentSelectionReasonCode.FocusedFullDocumentFallback,
                    Reason = string.IsNullOrWhiteSpace(options.Selector)
                        ? "Focused mode kept the full document because no preferred content element was found."
                        : $"Focused mode kept the full document because selector '{options.Selector}' did not match and no semantic fallback was found.",
                    Html = html
                };
        }
    }

    private static bool LooksLikeSemanticContentSelector(string? selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return false;
        }

        string normalized = selector!.Trim().ToLowerInvariant();
        return normalized is "main"
            or "[role='main']"
            or "[role=\"main\"]"
            or "#main"
            or "#main-content"
            or ".main-content"
            or ".site-main"
            or "#content"
            or ".content"
            or ".entry-content"
            or ".post-content"
            or "article";
    }

    private static IList<HtmlCrawlContentComparison> BuildContentComparisons(string html, HtmlCrawlOptions options) {
        List<HtmlCrawlContentComparison> comparisons = new();
        foreach (HtmlCrawlContentMode mode in new[] { HtmlCrawlContentMode.Raw, HtmlCrawlContentMode.Focused, HtmlCrawlContentMode.Reader }) {
            HtmlCrawlOptions comparisonOptions = options.Clone();
            comparisonOptions.ContentMode = mode;
            ContentSelectionResult selection = SelectContent(html, comparisonOptions);
            string selectedHtml = ApplyContentCleanup(selection.Html, comparisonOptions);
            string text = HtmlParserToText.ConvertToText(PrepareHtmlForTextExtraction(selectedHtml, comparisonOptions));
            comparisons.Add(new HtmlCrawlContentComparison {
                Mode = mode,
                ReasonCode = selection.ReasonCode,
                Reason = selection.Reason,
                ElementSelectorHint = BuildElementSelectorHint(selection.Element),
                WordCount = CountWords(text),
                CharacterCount = text.Length,
                Summary = BuildSummary(text),
                Score = selection.Score,
                ReaderCandidateCount = selection.ReaderCandidateCount,
                ReaderRootElementSelectorHint = selection.ReaderRootElementSelectorHint
            });
        }

        return comparisons;
    }

    private static IList<HtmlCrawlContentComparison> CloneContentComparisons(IEnumerable<HtmlCrawlContentComparison> comparisons) {
        return comparisons.Select(comparison => new HtmlCrawlContentComparison {
            Mode = comparison.Mode,
            ReasonCode = comparison.ReasonCode,
            Reason = comparison.Reason,
            ElementSelectorHint = comparison.ElementSelectorHint,
            WordCount = comparison.WordCount,
            CharacterCount = comparison.CharacterCount,
            Summary = comparison.Summary,
            Score = comparison.Score,
            ReaderCandidateCount = comparison.ReaderCandidateCount,
            ReaderRootElementSelectorHint = comparison.ReaderRootElementSelectorHint
        }).ToList();
    }

    private static HtmlCrawlContentComparison? GetBestContentComparison(IEnumerable<HtmlCrawlContentComparison> comparisons) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        int maxWordCount = comparisonList.Max(comparison => comparison.WordCount);
        return comparisonList
            .Where(comparison => comparison.WordCount >= maxWordCount - 10)
            .OrderBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .FirstOrDefault();
    }

    private static HtmlCrawlContentComparison? GetRunnerUpContentComparison(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison) {
        if (bestComparison == null) {
            return null;
        }

        return comparisons
            .Where(comparison => comparison.Mode != bestComparison.Mode)
            .OrderByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .FirstOrDefault();
    }

    private static string? BuildContentComparisonDeltaSummary(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison = null) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        bestComparison ??= GetBestContentComparison(comparisonList);
        if (bestComparison == null) {
            return null;
        }

        return string.Join(" | ", comparisonList
            .OrderBy(comparison => comparison.Mode == bestComparison.Mode ? 0 : 1)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .Select(comparison => {
                int delta = comparison.WordCount - bestComparison.WordCount;
                string formattedDelta = delta > 0
                    ? $"+{delta.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return $"{comparison.Mode} {formattedDelta}";
            }));
    }

    private static string? BuildContentComparisonPreviewSummary(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison = null) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        bestComparison ??= GetBestContentComparison(comparisonList);
        return string.Join(" | ", comparisonList
            .OrderBy(comparison => bestComparison != null && comparison.Mode == bestComparison.Mode ? 0 : 1)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .Select(comparison => {
                string summary = BuildCompactComparisonSummary(comparison.Summary);
                string selectorHint = string.IsNullOrWhiteSpace(comparison.ElementSelectorHint)
                    ? string.Empty
                    : $" @ {comparison.ElementSelectorHint}";
                return $"{comparison.Mode} {comparison.WordCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}w{selectorHint}: {summary}";
            }));
    }

    private static string BuildCompactComparisonSummary(string? summary) {
        if (string.IsNullOrWhiteSpace(summary)) {
            return "(no summary)";
        }

        const int maxLength = 56;
        string normalized = NormalizeWhitespace(summary);
        if (normalized.Length <= maxLength) {
            return normalized;
        }

        int cut = normalized.LastIndexOf(' ', maxLength);
        if (cut < maxLength / 2) {
            cut = maxLength;
        }

        return normalized.Substring(0, cut).TrimEnd() + "...";
    }

    private static int GetContentModePreference(HtmlCrawlContentMode mode) {
        return mode switch {
            HtmlCrawlContentMode.Reader => 0,
            HtmlCrawlContentMode.Focused => 1,
            _ => 2
        };
    }

    private static bool TrySelectPreferredContentElement(IDocument document, out IElement? element) {
        foreach (string selector in ContentFallbackSelectors) {
            element = document.QuerySelector(selector);
            if (element != null) {
                return true;
            }
        }

        element = null;
        return false;
    }

    private static ContentSelectionResult SelectReaderContent(IDocument document, string html, HtmlCrawlOptions options) {
        IElement? root = null;
        string rootDescription = "document body";
        if (!string.IsNullOrWhiteSpace(options.Selector)) {
            root = document.QuerySelector(options.Selector!);
            if (root != null) {
                rootDescription = $"configured selector '{options.Selector}'";
            } else if (LooksLikeSemanticContentSelector(options.Selector) && TrySelectPreferredContentElement(document, out IElement? fallback)) {
                root = fallback;
                rootDescription = $"semantic fallback {DescribeElement(fallback!)}";
            }
        }

        root ??= document.Body;
        if (root != null && rootDescription == "document body" && !ReferenceEquals(root, document.Body)) {
            rootDescription = $"selected root {DescribeElement(root)}";
        }
        if (root == null) {
            root = document.DocumentElement;
            rootDescription = "document root";
        }

        if (root == null) {
            return new ContentSelectionResult {
                ModeUsed = HtmlCrawlContentMode.Reader,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.ReaderRootFallback,
                Reason = "Reader mode kept the full document because no DOM root was available.",
                Html = html
            };
        }

        IElement selected = FindBestReaderCandidate(root, options, out bool usedRootFallback, out int candidateCount, out double? selectedScore) ?? root;
        if (usedRootFallback) {
            return BuildContentSelectionResult(
                HtmlCrawlContentMode.Reader,
                HtmlCrawlContentSelectionReasonCode.ReaderRootFallback,
                $"Reader mode started from {rootDescription} and kept {DescribeElement(selected)} because no stronger article-like candidate met the minimum score of {options.ReaderMinimumScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.",
                selected,
                selectedScore,
                candidateCount,
                BuildElementSelectorHint(root));
        }

        return BuildContentSelectionResult(
            HtmlCrawlContentMode.Reader,
            HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate,
            $"Reader mode started from {rootDescription} and selected the best-scoring content block {DescribeElement(selected)} with score {selectedScore?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}.",
            selected,
            selectedScore,
            candidateCount,
            BuildElementSelectorHint(root));
    }

    private static ContentSelectionResult BuildContentSelectionResult(
        HtmlCrawlContentMode mode,
        HtmlCrawlContentSelectionReasonCode reasonCode,
        string reason,
        IElement element,
        double? score = null,
        int readerCandidateCount = 0,
        string? readerRootElementSelectorHint = null) {
        return new ContentSelectionResult {
            ModeUsed = mode,
            ReasonCode = reasonCode,
            Reason = reason,
            Element = element,
            Html = element.OuterHtml,
            Score = score,
            ReaderCandidateCount = readerCandidateCount,
            ReaderRootElementSelectorHint = readerRootElementSelectorHint
        };
    }

    private static IList<string> GetElementClassNames(IElement? element) {
        if (element == null || element.ClassList.Length == 0) {
            return new List<string>();
        }

        return element.ClassList
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? BuildElementSelectorHint(IElement? element) {
        if (element == null) {
            return null;
        }

        StringBuilder selector = new();
        selector.Append(element.LocalName);
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            selector.Append('#').Append(element.Id);
        }

        foreach (string className in element.ClassList.Where(className => !string.IsNullOrWhiteSpace(className)).Take(3)) {
            selector.Append('.').Append(className);
        }

        return selector.ToString();
    }

    private static string DescribeElement(IElement element) {
        return $"<{BuildElementSelectorHint(element) ?? element.LocalName}>";
    }

    private static IElement? FindBestReaderCandidate(
        IElement root,
        HtmlCrawlOptions options,
        out bool usedRootFallback,
        out int candidateCount,
        out double? selectedScore) {
        List<IElement> candidates = new() { root };
        candidates.AddRange(root.QuerySelectorAll("article, main, section, div"));
        List<IElement> distinctCandidates = candidates.Distinct().ToList();
        candidateCount = distinctCandidates.Count;

        IElement? best = null;
        double bestScore = double.MinValue;
        foreach (IElement candidate in distinctCandidates) {
            double score = ScoreReaderCandidate(candidate, options);
            if (score > bestScore) {
                best = candidate;
                bestScore = score;
            }
        }

        double rootScore = ScoreReaderCandidate(root, options);
        if (best == null) {
            usedRootFallback = true;
            selectedScore = NormalizeContentScore(rootScore);
            return root;
        }

        usedRootFallback = bestScore < options.ReaderMinimumScore;
        selectedScore = usedRootFallback
            ? NormalizeContentScore(rootScore)
            : NormalizeContentScore(bestScore);
        return usedRootFallback ? root : best;
    }

    private static double? NormalizeContentScore(double score) {
        return double.IsNegativeInfinity(score) || score == double.MinValue ? null : score;
    }

    private static double ScoreReaderCandidate(IElement element, HtmlCrawlOptions options) {
        if (!IsReaderCandidateElement(element)) {
            return double.MinValue;
        }

        string text = element.TextContent ?? string.Empty;
        int wordCount = CountWords(text);
        if (wordCount < options.ReaderMinimumWordCount) {
            return double.MinValue;
        }

        int paragraphCount = element.QuerySelectorAll("p").Length;
        int headingCount = element.QuerySelectorAll("h1, h2, h3").Length;
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int listItemCount = element.QuerySelectorAll("li").Length;
        int codeBlockCount = element.QuerySelectorAll("pre, code").Length;
        int tableCount = element.QuerySelectorAll("table").Length;
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));
        double linkDensity = (double)linkWordCount / Math.Max(1, wordCount);

        double score = wordCount;
        score += paragraphCount * 18;
        score += headingCount * 20;
        score += codeBlockCount * 14;
        score += tableCount * 10;

        string tagName = element.TagName.ToLowerInvariant();
        if (tagName == "article") {
            score += 35;
        } else if (tagName == "main") {
            score += 20;
        } else if (tagName == "section") {
            score += 10;
        }

        if (linkDensity >= 0.75) {
            score -= 80;
        } else if (linkDensity >= 0.55) {
            score -= 40;
        }

        if (listItemCount >= 5 && paragraphCount == 0) {
            score -= 45;
        }

        if (ContainsBoilerplateSignals(element)) {
            score -= 55;
        }

        if (IsLinkDenseBoilerplateBlock(element)) {
            score -= 80;
        }

        return score;
    }

    private static bool IsReaderCandidateElement(IElement element) {
        string tagName = element.TagName.ToLowerInvariant();
        return tagName is "article" or "main" or "section" or "div";
    }

    private static string PrepareHtmlForTextExtraction(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
                RemoveHiddenElements(document);
            }
            if (options.SmartContentCleanup) {
                StripBoilerplateElements(document, options);
            }
            RemoveConfiguredElements(document, options);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_text\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_text");
        if (wrapper == null) {
            return html;
        }

        if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
            RemoveHiddenElements(wrapper);
        }
        if (options.SmartContentCleanup) {
            StripBoilerplateElements(wrapper, options);
        }
        RemoveConfiguredElements(wrapper, options);
        return wrapper.InnerHtml;
    }

    private static string ApplyContentCleanup(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        bool hasConfiguredCleanup = options.ExcludeSelectors.Count > 0 || options.ExcludeClasses.Count > 0 || options.ExcludeIds.Count > 0;
        bool filterHiddenContent = options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden;
        if (!hasConfiguredCleanup && !options.SmartContentCleanup && !filterHiddenContent) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
                RemoveHiddenElements(document);
            }
            if (options.SmartContentCleanup) {
                StripBoilerplateElements(document, options);
            }
            RemoveConfiguredElements(document, options);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_exclude\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_exclude");
        if (wrapper == null) {
            return html;
        }

        if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
            RemoveHiddenElements(wrapper);
        }
        if (options.SmartContentCleanup) {
            StripBoilerplateElements(wrapper, options);
        }
        RemoveConfiguredElements(wrapper, options);
        return wrapper.InnerHtml;
    }

    private static void RemoveHiddenElements(IParentNode container) {
        foreach (IElement element in container.QuerySelectorAll("[hidden],[aria-hidden],[style],input[type='hidden'],[data-htmltinkerx-hidden='true']").ToArray()) {
            if (ShouldRemoveHiddenElement(element)) {
                element.Remove();
            }
        }
    }

    private static bool ShouldRemoveHiddenElement(IElement element) {
        if (element == null) {
            return false;
        }

        if (element.HasAttribute("hidden")) {
            return true;
        }

        if (string.Equals(element.GetAttribute("data-htmltinkerx-hidden"), "true", StringComparison.OrdinalIgnoreCase)) {
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
        return normalizedStyle.Contains("display:none", StringComparison.Ordinal)
               || normalizedStyle.Contains("visibility:hidden", StringComparison.Ordinal)
               || normalizedStyle.Contains("content-visibility:hidden", StringComparison.Ordinal);
    }

    internal static Task MarkRenderedHiddenElementsAsync(IPage page) {
        return page.EvaluateAsync(
            """
            () => {
                const hiddenAttributeName = 'data-htmltinkerx-hidden';
                for (const element of document.querySelectorAll(`[${hiddenAttributeName}]`)) {
                    element.removeAttribute(hiddenAttributeName);
                }

                const shouldMarkHidden = (element) => {
                    if (!(element instanceof Element)) {
                        return false;
                    }

                    const tagName = (element.tagName || '').toLowerCase();
                    if (tagName === 'html' || tagName === 'head' || tagName === 'body') {
                        return false;
                    }

                    if (element.hasAttribute('hidden')) {
                        return true;
                    }

                    if ((element.getAttribute('aria-hidden') || '').toLowerCase() === 'true') {
                        return true;
                    }

                    if (tagName === 'input' && (element.getAttribute('type') || '').toLowerCase() === 'hidden') {
                        return true;
                    }

                    const style = window.getComputedStyle(element);
                    if (!style) {
                        return false;
                    }

                    return style.display === 'none'
                        || style.visibility === 'hidden'
                        || style.visibility === 'collapse'
                        || style.contentVisibility === 'hidden';
                };

                for (const element of document.querySelectorAll('*')) {
                    if (shouldMarkHidden(element)) {
                        element.setAttribute(hiddenAttributeName, 'true');
                    }
                }
            }
            """);
    }

    private static void StripBoilerplateElements(IParentNode container, HtmlCrawlOptions options) {
        foreach (IElement element in container.QuerySelectorAll(
                     "script,style,noscript,svg,header,nav,footer,aside,[role='banner'],[role='navigation'],[role='contentinfo'],[role='search'],form[role='search'],.wpml-ls,.sharing-popup,.post-footer-sharing,.socials-sharing,.gem-pagination,.menu-toggle,.minisearch,.skip-link,.skip-link-screen-reader-text").ToArray()) {
            if (ShouldPreserveStructuredContentElement(element)
                || ShouldPreserveOfflineExecutableElement(element, options)) {
                continue;
            }

            element.Remove();
        }

        if (!options.SmartContentCleanup) {
            return;
        }

        foreach (IElement element in container.QuerySelectorAll("*").Where(ShouldRemoveBoilerplateElement).ToArray()) {
            element.Remove();
        }
    }

    private static bool ShouldPreserveStructuredContentElement(IElement element) {
        if (element == null) {
            return false;
        }

        return LooksLikeStructuredCalloutElement(element) || LooksLikeMediaNoscriptFallbackElement(element);
    }

    private static bool ShouldPreserveOfflineExecutableElement(IElement element, HtmlCrawlOptions options) {
        if (element == null || options == null) {
            return false;
        }

        if (!options.DownloadAssets || !options.RewriteAssetReferencesToLocal) {
            return false;
        }

        return element.TagName.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase)
               || element.TagName.Equals("STYLE", StringComparison.OrdinalIgnoreCase)
               || element.TagName.Equals("LINK", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMediaNoscriptFallbackElement(IElement element) {
        return TryGetNoscriptFallbackWrapper(element, out _);
    }

    private static IEnumerable<string> EnumerateNoscriptHtmlCandidates(IElement element) {
        string innerHtml = element.InnerHtml;
        if (!string.IsNullOrWhiteSpace(innerHtml)) {
            yield return innerHtml;
        }

        string textContent = element.TextContent;
        if (!string.IsNullOrWhiteSpace(textContent) && !string.Equals(textContent, innerHtml, StringComparison.Ordinal)) {
            yield return textContent;
        }
    }

    private static void RemoveConfiguredElements(IParentNode container, HtmlCrawlOptions options) {
        RemoveMatchingSelectors(container, options.ExcludeSelectors);
        RemoveMatchingClasses(container, options.ExcludeClasses);
        RemoveMatchingIds(container, options.ExcludeIds);
    }

    private static void RemoveMatchingSelectors(IParentNode container, IEnumerable<string>? selectors) {
        if (selectors == null) {
            return;
        }

        foreach (string selector in selectors.Where(selector => !string.IsNullOrWhiteSpace(selector)).Select(selector => selector.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll(selector).ToArray()) {
                element.Remove();
            }
        }
    }

    private static void RemoveMatchingClasses(IParentNode container, IEnumerable<string>? classNames) {
        if (classNames == null) {
            return;
        }

        foreach (string className in classNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimStart('.')).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll("." + CssEscapeIdentifier(className)).ToArray()) {
                element.Remove();
            }
        }
    }

    private static void RemoveMatchingIds(IParentNode container, IEnumerable<string>? ids) {
        if (ids == null) {
            return;
        }

        foreach (string id in ids.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimStart('#')).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll("#" + CssEscapeIdentifier(id)).ToArray()) {
                element.Remove();
            }
        }
    }

    private static bool ShouldRemoveBoilerplateElement(IElement element) {
        if (!IsLowValueContainer(element)) {
            return false;
        }

        bool hasSignalToken = ContainsBoilerplateSignals(element);
        if (hasSignalToken && IsLikelyLowValueContentBlock(element)) {
            return true;
        }

        return IsLinkDenseBoilerplateBlock(element);
    }

    private static bool IsLowValueContainer(IElement element) {
        string tagName = element.TagName.ToLowerInvariant();
        if (tagName is "body" or "html" or "main" or "article") {
            return false;
        }

        return tagName is "div" or "section" or "aside" or "nav" or "ul" or "ol" or "details" or "form";
    }

    private static string GetBoilerplateSignalText(IElement element) {
        return string.Join(" ",
            element.Id ?? string.Empty,
            element.ClassName ?? string.Empty,
            element.GetAttribute("aria-label") ?? string.Empty,
            element.GetAttribute("role") ?? string.Empty,
            element.GetAttribute("data-testid") ?? string.Empty);
    }

    private static bool ContainsBoilerplateSignals(IElement element) {
        string combined = GetBoilerplateSignalText(element);
        if (string.IsNullOrWhiteSpace(combined)) {
            return false;
        }

        string normalized = combined.ToLowerInvariant();
        return BoilerplateSignalTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static bool IsLikelyLowValueContentBlock(IElement element) {
        if (element.QuerySelector("pre, code, table") != null) {
            return false;
        }

        int headingCount = element.QuerySelectorAll("h1, h2, h3").Length;
        int paragraphCount = element.QuerySelectorAll("p").Length;
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int wordCount = CountWords(element.TextContent);
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));

        if (headingCount > 0 && wordCount >= 60) {
            return false;
        }

        if (paragraphCount >= 2 && wordCount >= 50 && linkCount <= 6) {
            return false;
        }

        if (wordCount <= 12) {
            return true;
        }

        if (linkCount >= 3 && linkWordCount >= Math.Max(8, wordCount / 2)) {
            return true;
        }

        return false;
    }

    private static bool IsLinkDenseBoilerplateBlock(IElement element) {
        if (element.QuerySelector("pre, code, table, img") != null) {
            return false;
        }

        int wordCount = CountWords(element.TextContent);
        if (wordCount == 0) {
            return true;
        }

        int linkCount = element.QuerySelectorAll("a[href]").Length;
        if (linkCount < 3) {
            return false;
        }

        int listItemCount = element.QuerySelectorAll("li").Length;
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));
        double linkDensity = (double)linkWordCount / Math.Max(1, wordCount);

        if (linkDensity >= 0.75 && wordCount <= 90) {
            return true;
        }

        if (listItemCount >= 4 && linkDensity >= 0.55 && wordCount <= 140) {
            return true;
        }

        return false;
    }

    private static string CssEscapeIdentifier(string value) {
        StringBuilder builder = new(value.Length);
        foreach (char character in value) {
            if (char.IsLetterOrDigit(character) || character is '-' or '_') {
                builder.Append(character);
            } else {
                builder.Append('\\').Append(character);
            }
        }

        return builder.ToString();
    }

}
