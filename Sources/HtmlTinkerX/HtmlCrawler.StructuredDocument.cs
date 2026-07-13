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
    private static HtmlCrawlStructuredDocument BuildStructuredDocument(
        HtmlCrawlPage page,
        PageSearchMetadata searchMetadata,
        string text,
        string markdown) {
        return new HtmlCrawlStructuredDocument {
            Url = page.Url,
            RequestedUrl = page.RequestedUrl,
            ParentUrl = page.ParentUrl,
            CanonicalUrl = page.CanonicalUrl,
            Depth = page.Depth,
            Title = page.Title,
            ContentType = page.ContentType,
            Rendered = page.Rendered,
            RenderMode = page.RenderMode,
            RenderReasonCode = page.RenderReasonCode,
            ContentModeUsed = page.ContentModeUsed,
            ContentSelectionReasonCode = page.ContentSelectionReasonCode,
            ContentSelectionReason = page.ContentSelectionReason,
            ContentElementSelectorHint = page.ContentElementSelectorHint,
            WordCount = searchMetadata.WordCount,
            CharacterCount = searchMetadata.CharacterCount,
            ChunkCount = searchMetadata.ChunkCount,
            LinkCount = page.Links.Count,
            AssetCount = page.AssetUrls.Count,
            InteractionCount = page.AppliedInteractions.Count,
            Summary = searchMetadata.Summary,
            Headings = new List<string>(searchMetadata.Headings),
            Keywords = new List<string>(searchMetadata.Keywords),
            Links = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AppliedInteractions = new List<string>(page.AppliedInteractions),
            Text = text,
            Markdown = markdown
        };
    }

    private static HtmlCrawlStructuredMetadata BuildStructuredMetadata(
        HtmlCrawlPage page,
        IDocument document,
        IReadOnlyCollection<HtmlMetaTag> metaTags,
        HtmlOpenGraph openGraph) {
        return new HtmlCrawlStructuredMetadata {
            Title = page.Title ?? FindOpenGraphValue(openGraph, "title"),
            Description = FindMetaContent(metaTags, "description")
                ?? FindOpenGraphValue(openGraph, "description"),
            CanonicalUrl = page.CanonicalUrl,
            Language = document.DocumentElement?.GetAttribute("lang"),
            SiteName = FindOpenGraphValue(openGraph, "site_name")
                ?? FindMetaContent(metaTags, "application-name"),
            Type = FindOpenGraphValue(openGraph, "type"),
            Author = FindMetaContent(metaTags, "author", "article:author"),
            PublishedTime = FindMetaContent(metaTags, "article:published_time", "published_time", "pubdate"),
            ModifiedTime = FindMetaContent(metaTags, "article:modified_time", "last-modified", "modified_time"),
            Robots = FindMetaContent(metaTags, "robots"),
            Generator = FindMetaContent(metaTags, "generator"),
            ImageUrl = FindOpenGraphValue(openGraph, "image"),
            Keywords = SplitMetadataKeywords(FindMetaContent(metaTags, "keywords")),
            MetaTagCount = metaTags.Count,
            OpenGraphPropertyCount = openGraph.Properties.Count
        };
    }

    private static HtmlCrawlStructuredLayout BuildStructuredLayout(IDocument document) {
        List<HtmlCrawlStructuredRegion> regions = new();
        HashSet<IElement> seenElements = new();

        foreach ((string kind, string selector) in StructuredRegionSelectors) {
            IEnumerable<IElement> matches = document.QuerySelectorAll(selector)
                .Where(element => element != null && seenElements.Add(element))
                .Where(element => !string.Equals(kind, "Navigation", StringComparison.OrdinalIgnoreCase) || !LooksLikeBreadcrumbElement(element))
                .Where(ShouldIncludeStructuredRegion)
                .OrderByDescending(element => CountWords(element.TextContent))
                .ThenBy(element => BuildElementSelectorHint(element), StringComparer.OrdinalIgnoreCase)
                .Take(4);

            foreach (IElement element in matches) {
                regions.Add(BuildStructuredRegion(kind, element));
            }
        }

        return new HtmlCrawlStructuredLayout {
            Regions = regions
                .OrderBy(region => GetStructuredRegionKindOrder(region.Kind))
                .ThenByDescending(region => region.WordCount)
                .ThenBy(region => region.SelectorHint, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            HeaderCount = regions.Count(region => string.Equals(region.Kind, "Header", StringComparison.OrdinalIgnoreCase)),
            NavigationCount = regions.Count(region => string.Equals(region.Kind, "Navigation", StringComparison.OrdinalIgnoreCase)),
            MainCount = regions.Count(region => string.Equals(region.Kind, "Main", StringComparison.OrdinalIgnoreCase)),
            ArticleCount = regions.Count(region => string.Equals(region.Kind, "Article", StringComparison.OrdinalIgnoreCase)),
            AsideCount = regions.Count(region => string.Equals(region.Kind, "Aside", StringComparison.OrdinalIgnoreCase)),
            FooterCount = regions.Count(region => string.Equals(region.Kind, "Footer", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static HtmlCrawlStructuredRegion BuildStructuredRegion(string kind, IElement element) {
        string text = NormalizeWhitespace(element.TextContent);
        return new HtmlCrawlStructuredRegion {
            Kind = kind,
            Tag = element.LocalName,
            Id = string.IsNullOrWhiteSpace(element.Id) ? null : element.Id,
            Classes = GetElementClassNames(element),
            SelectorHint = BuildElementSelectorHint(element),
            Role = element.GetAttribute("role"),
            AriaLabel = element.GetAttribute("aria-label"),
            Summary = BuildSummary(text),
            WordCount = CountWords(text),
            LinkCount = element.QuerySelectorAll("a[href]").Length,
            HeadingCount = element.QuerySelectorAll("h1, h2, h3, h4, h5, h6").Length,
            LinkLabels = element.QuerySelectorAll("a[href]")
                .Select(anchor => NormalizeWhitespace(anchor.TextContent))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList(),
            IsLikelyBoilerplate = ContainsBoilerplateSignals(element) || IsLinkDenseBoilerplateBlock(element)
        };
    }

    private static bool ShouldIncludeStructuredRegion(IElement element) {
        if (element == null) {
            return false;
        }

        string text = NormalizeWhitespace(element.TextContent);
        int wordCount = CountWords(text);
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int headingCount = element.QuerySelectorAll("h1, h2, h3, h4, h5, h6").Length;
        return wordCount > 0 || linkCount > 0 || headingCount > 0;
    }

    private static bool LooksLikeBreadcrumbElement(IElement element) {
        if (element == null) {
            return false;
        }

        string selectorHint = BuildElementSelectorHint(element) ?? string.Empty;
        string ariaLabel = element.GetAttribute("aria-label") ?? string.Empty;
        string itemType = element.GetAttribute("itemtype") ?? string.Empty;
        return selectorHint.IndexOf("breadcrumb", StringComparison.OrdinalIgnoreCase) >= 0
            || ariaLabel.IndexOf("breadcrumb", StringComparison.OrdinalIgnoreCase) >= 0
            || itemType.IndexOf("BreadcrumbList", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int GetStructuredRegionKindOrder(string? kind) {
        return kind?.ToLowerInvariant() switch {
            "header" => 0,
            "navigation" => 1,
            "main" => 2,
            "article" => 3,
            "aside" => 4,
            "footer" => 5,
            _ => 6
        };
    }

    private static List<HtmlCrawlStructuredCodeBlock> BuildStructuredCodeBlocks(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCodeBlock> blocks = new();
        HashSet<IElement> seen = new();
        IEnumerable<IElement> elements = selectedDocument.QuerySelectorAll("pre")
            .Concat(selectedDocument.QuerySelectorAll("code[class*='language'], code[class*='lang-'], code[data-language], code[lang], code[type], samp, kbd")
                .Where(element => element.Closest("pre") == null));

        foreach (IElement element in elements) {
            if (element == null || !seen.Add(element)) {
                continue;
            }

            IElement sourceElement = string.Equals(element.LocalName, "pre", StringComparison.OrdinalIgnoreCase)
                ? element.QuerySelector("code") ?? element
                : element;
            string code = NormalizeCodeBlockText(sourceElement.TextContent);
            if (string.IsNullOrWhiteSpace(code)) {
                continue;
            }

            string[] lines = code.Split(new[] { '\n' }, StringSplitOptions.None);
            blocks.Add(new HtmlCrawlStructuredCodeBlock {
                Language = DetectCodeBlockLanguage(sourceElement),
                Code = code,
                LineCount = lines.Length,
                CharacterCount = code.Length,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return blocks;
    }

    private static List<HtmlCrawlStructuredCodeSample> BuildStructuredCodeSamples(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCodeSample> samples = new();
        HashSet<IElement> seen = new();
        foreach (IElement element in selectedDocument.QuerySelectorAll("pre")
                     .Concat(selectedDocument.QuerySelectorAll("code[class*='language'], code[class*='lang-'], code[data-language], code[lang], code[type], samp, kbd")
                         .Where(item => item.Closest("pre") == null))) {
            if (element == null || !seen.Add(element)) {
                continue;
            }

            IElement sourceElement = string.Equals(element.LocalName, "pre", StringComparison.OrdinalIgnoreCase)
                ? element.QuerySelector("code") ?? element
                : element;
            string code = NormalizeCodeBlockText(sourceElement.TextContent);
            if (string.IsNullOrWhiteSpace(code)) {
                continue;
            }

            string? heading = FindNearbyHeadingText(element);
            string? language = DetectCodeBlockLanguage(sourceElement);
            string kind = DetectStructuredCodeSampleKind(code, language);
            string? method = null;
            string? path = null;
            TryParseApiMethodAndPath(code, out method, out path);
            if (string.IsNullOrWhiteSpace(method)
                && string.IsNullOrWhiteSpace(path)
                && LooksLikeRequestPayloadHeading(heading)) {
                string? apiHeading = FindNearbyApiHeadingText(element);
                if (!string.IsNullOrWhiteSpace(apiHeading)) {
                    TryParseApiMethodAndPath(apiHeading!, out method, out path);
                }
            }

            samples.Add(new HtmlCrawlStructuredCodeSample {
                Title = BuildStructuredCodeSampleTitle(heading, kind, method, path, language),
                Heading = heading,
                Language = language,
                Kind = kind,
                Code = code,
                Method = method,
                Path = path,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return samples;
    }

    private static List<HtmlCrawlStructuredBreadcrumbTrail> BuildStructuredBreadcrumbs(IDocument document, string? pageUrl) {
        List<HtmlCrawlStructuredBreadcrumbTrail> trails = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Uri? baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? resolvedBaseUri) ? resolvedBaseUri : null;

        foreach (IElement container in document.QuerySelectorAll("[aria-label*='breadcrumb' i], nav.breadcrumb, .breadcrumb, .breadcrumbs, [data-breadcrumb], [data-breadcrumbs], ol[itemtype*='BreadcrumbList' i], ul[itemtype*='BreadcrumbList' i]")) {
            HtmlCrawlStructuredBreadcrumbTrail? trail = BuildStructuredBreadcrumbTrail(container, baseUri);
            if (trail == null || trail.Items.Count == 0) {
                continue;
            }

            string key = string.Join(">", trail.Labels);
            if (!seen.Add(key)) {
                continue;
            }

            trails.Add(trail);
        }

        return trails;
    }

    private static HtmlCrawlStructuredBreadcrumbTrail? BuildStructuredBreadcrumbTrail(IElement container, Uri? baseUri) {
        IEnumerable<IElement> candidates = container.QuerySelectorAll("li").Length > 0
            ? container.QuerySelectorAll("li")
            : container.QuerySelectorAll("a[href], [aria-current='page'], span[itemprop='name'], strong, span");

        List<HtmlCrawlStructuredBreadcrumbItem> items = new();
        foreach (IElement candidate in candidates) {
            IElement? anchor = string.Equals(candidate.LocalName, "a", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : candidate.QuerySelector("a[href]");
            string label = NormalizeWhitespace(anchor?.TextContent ?? candidate.TextContent);
            if (string.IsNullOrWhiteSpace(label)) {
                continue;
            }

            if (items.Count > 0 && string.Equals(items[items.Count - 1].Label, label, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string? url = TryResolveStructuredHref(baseUri, anchor?.GetAttribute("href"));
            bool isCurrent = string.Equals(candidate.GetAttribute("aria-current"), "page", StringComparison.OrdinalIgnoreCase)
                || (anchor == null && candidates.Count() > 1)
                || (string.IsNullOrWhiteSpace(url) && items.Count > 0);
            items.Add(new HtmlCrawlStructuredBreadcrumbItem {
                Label = label,
                Url = url,
                IsCurrent = isCurrent
            });
        }

        if (items.Count < 2) {
            return null;
        }

        return new HtmlCrawlStructuredBreadcrumbTrail {
            Items = items,
            Labels = items.Select(item => item.Label).ToList(),
            Urls = items.Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .Select(item => item.Url!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CurrentLabel = items.LastOrDefault(item => item.IsCurrent)?.Label ?? items[items.Count - 1].Label,
            SelectorHint = BuildElementSelectorHint(container)
        };
    }

    private static List<HtmlCrawlStructuredFaqItem> BuildStructuredFaqItems(IDocument selectedDocument) {
        List<HtmlCrawlStructuredFaqItem> items = new();
        HashSet<string> seenQuestions = new(StringComparer.OrdinalIgnoreCase);

        foreach (IElement questionElement in selectedDocument.QuerySelectorAll("[itemscope][itemtype*='Question' i], [itemprop='mainEntity'][itemscope]")) {
            string question = NormalizeWhitespace(questionElement.QuerySelector("[itemprop='name']")?.TextContent);
            IElement? answerElement = questionElement.QuerySelector("[itemprop='acceptedAnswer'] [itemprop='text'], [itemprop='acceptedAnswer']");
            string answer = NormalizeWhitespace(answerElement?.TextContent);
            string answerMarkdown = answerElement == null ? string.Empty : ConvertSelectedHtmlToMarkdown(answerElement.InnerHtml, null);
            AddStructuredFaqItem(items, seenQuestions, question, answer, answerMarkdown, "Microdata", questionElement);
        }

        foreach (IElement details in selectedDocument.QuerySelectorAll("details")) {
            string question = NormalizeWhitespace(details.QuerySelector("summary")?.TextContent);
            IElement clone = (IElement)details.Clone(true);
            foreach (IElement summary in clone.QuerySelectorAll("summary").ToArray()) {
                summary.Remove();
            }

            string answer = NormalizeWhitespace(clone.TextContent);
            string answerMarkdown = string.IsNullOrWhiteSpace(clone.InnerHtml) ? string.Empty : ConvertSelectedHtmlToMarkdown(clone.InnerHtml, null);
            AddStructuredFaqItem(items, seenQuestions, question, answer, answerMarkdown, "Details", details);
        }

        return items;
    }

    private static void AddStructuredFaqItem(
        IList<HtmlCrawlStructuredFaqItem> items,
        ISet<string> seenQuestions,
        string question,
        string answer,
        string answerMarkdown,
        string source,
        IElement element) {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer) || !seenQuestions.Add(question)) {
            return;
        }

        items.Add(new HtmlCrawlStructuredFaqItem {
            Question = question,
            Answer = answer,
            AnswerMarkdown = answerMarkdown,
            Source = source,
            SelectorHint = BuildElementSelectorHint(element)
        });
    }

    private static List<HtmlCrawlStructuredSpecTable> BuildStructuredSpecTables(IDocument selectedDocument, IReadOnlyList<HtmlTableResult> tables) {
        List<HtmlCrawlStructuredSpecTable> specTables = new();
        IHtmlCollection<IElement> tableElements = selectedDocument.QuerySelectorAll("table");
        for (int index = 0; index < Math.Min(tableElements.Length, tables.Count); index++) {
            IElement tableElement = tableElements[index];
            HtmlTableResult table = tables[index];
            if (!LooksLikeStructuredSpecTable(tableElement, table)) {
                continue;
            }

            string? title = NormalizeWhitespace(tableElement.QuerySelector("caption")?.TextContent);
            if (string.IsNullOrWhiteSpace(title)) {
                title = FindNearbyHeadingText(tableElement);
            }

            List<HtmlCrawlStructuredSpecItem> entries = BuildStructuredSpecEntries(table);
            if (entries.Count == 0) {
                continue;
            }

            HtmlCrawlStructuredSpecTable specTable = new() {
                TableIndex = index,
                Title = title,
                Headers = new List<string>(table.Metadata.Headers),
                Entries = entries,
                SelectorHint = BuildElementSelectorHint(tableElement)
            };
            foreach (HtmlCrawlStructuredSpecItem entry in entries) {
                if (!specTable.Properties.ContainsKey(entry.Name)) {
                    specTable.Properties[entry.Name] = entry.Value;
                }
            }

            specTables.Add(specTable);
        }

        return specTables;
    }

    private static List<HtmlCrawlStructuredSpecItem> BuildStructuredSpecEntries(HtmlTableResult table) {
        List<HtmlCrawlStructuredSpecItem> entries = new();
        foreach (Dictionary<string, string?> row in table.Data) {
            List<KeyValuePair<string, string?>> populated = row
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .ToList();
            if (populated.Count < 2) {
                continue;
            }

            HtmlCrawlStructuredSpecItem item = new() {
                Name = NormalizeWhitespace(populated[0].Value ?? populated[0].Key),
                Value = NormalizeWhitespace(populated[1].Value)
            };
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Value)) {
                continue;
            }

            entries.Add(item);
        }

        return entries;
    }

    private static bool LooksLikeStructuredSpecTable(IElement tableElement, HtmlTableResult table) {
        string selectorHint = BuildElementSelectorHint(tableElement) ?? string.Empty;
        string classes = table.Metadata.Classes ?? string.Empty;
        string headers = string.Join(" ", table.Metadata.Headers);
        string nearbyHeading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        bool looksLikeParameterTable = ContainsAnyToken(headers, "parameter", "required", "description", "default", "location")
            || ContainsAnyToken(nearbyHeading, "parameter", "request body", "query parameter", "path parameter", "header");
        if (looksLikeParameterTable) {
            return false;
        }

        bool classSignal = ContainsAnyToken(selectorHint, "spec", "feature", "attribute", "property", "parameter", "option", "config")
            || ContainsAnyToken(classes, "spec", "feature", "attribute", "property", "parameter", "option", "config");
        bool headerSignal = ContainsAnyToken(headers, "name", "property", "attribute", "setting", "option", "parameter", "value", "description", "details");
        bool twoColumn = table.Metadata.ColumnCount > 0 && table.Metadata.ColumnCount <= 2;
        bool rowShapeLooksLikePairs = table.Data.Count > 0 && table.Data.All(row => row.Count(item => !string.IsNullOrWhiteSpace(item.Value)) <= 2);
        return classSignal || (twoColumn && (headerSignal || rowShapeLooksLikePairs));
    }

    private static List<HtmlCrawlStructuredCallout> BuildStructuredCallouts(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCallout> callouts = new();
        HashSet<IElement> seen = new();
        foreach (IElement element in selectedDocument.QuerySelectorAll("aside, blockquote, div, section").Where(LooksLikeStructuredCalloutElement)) {
            if (element == null || !seen.Add(element) || element.Closest("details") != null) {
                continue;
            }

            string text = NormalizeWhitespace(element.TextContent);
            if (string.IsNullOrWhiteSpace(text)) {
                continue;
            }

            string? title = NormalizeWhitespace(element.QuerySelector("strong, b, h1, h2, h3, h4, h5, h6, .title, .heading")?.TextContent);
            callouts.Add(new HtmlCrawlStructuredCallout {
                Kind = DetectStructuredCalloutKind(element),
                Title = title,
                Text = text,
                Markdown = string.IsNullOrWhiteSpace(element.InnerHtml) ? string.Empty : ConvertSelectedHtmlToMarkdown(element.InnerHtml, null),
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return callouts;
    }

    private static bool LooksLikeStructuredCalloutElement(IElement element) {
        if (element == null) {
            return false;
        }

        string role = element.GetAttribute("role") ?? string.Empty;
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        return role.Equals("alert", StringComparison.OrdinalIgnoreCase)
            || role.Equals("note", StringComparison.OrdinalIgnoreCase)
            || role.Equals("status", StringComparison.OrdinalIgnoreCase)
            || ContainsAnyToken(hint, "note", "warning", "warn", "tip", "info", "danger", "error", "success", "important", "callout", "admonition", "caution");
    }

    private static string DetectStructuredCalloutKind(IElement element) {
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        string role = element.GetAttribute("role") ?? string.Empty;
        if (ContainsAnyToken(hint, "warning", "warn", "caution")) {
            return "warning";
        }
        if (ContainsAnyToken(hint, "danger", "error")) {
            return "danger";
        }
        if (ContainsAnyToken(hint, "tip", "success")) {
            return "tip";
        }
        if (ContainsAnyToken(hint, "important")) {
            return "important";
        }
        if (role.Equals("alert", StringComparison.OrdinalIgnoreCase)) {
            return "warning";
        }
        return "note";
    }

    private static List<HtmlCrawlStructuredPrimaryAction> BuildStructuredPrimaryActions(IDocument selectedDocument, string? pageUrl) {
        List<(int Score, HtmlCrawlStructuredPrimaryAction Action)> scored = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Uri? baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? baseUriResolved) ? baseUriResolved : null;

        foreach (IElement element in selectedDocument.QuerySelectorAll("a[href], button, input[type='submit'], input[type='button'], [role='button']")) {
            string label = GetStructuredActionLabel(element);
            if (string.IsNullOrWhiteSpace(label)) {
                continue;
            }

            int score = ScoreStructuredPrimaryAction(element, label);
            if (score <= 0) {
                continue;
            }

            string? url = string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase)
                ? TryResolveStructuredHref(baseUri, element.GetAttribute("href"))
                : null;
            string key = $"{label}|{url}|{element.LocalName}";
            if (!seen.Add(key)) {
                continue;
            }

            scored.Add((score, new HtmlCrawlStructuredPrimaryAction {
                Label = label,
                Url = url,
                Type = DetectStructuredActionType(element),
                Intent = DetectStructuredActionIntent(label, element),
                SelectorHint = BuildElementSelectorHint(element)
            }));
        }

        return scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Action.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Action)
            .Take(8)
            .ToList();
    }

    private static string GetStructuredActionLabel(IElement element) {
        string? inputValue = element.GetAttribute("value");
        return NormalizeWhitespace(inputValue ?? element.TextContent);
    }

    private static int ScoreStructuredPrimaryAction(IElement element, string label) {
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        int score = 0;
        if (ContainsAnyToken(hint, "primary", "cta", "button", "btn", "action")) {
            score += 3;
        }
        if (element.GetAttribute("role")?.Equals("button", StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(element.LocalName, "button", StringComparison.OrdinalIgnoreCase)) {
            score += 2;
        }
        if (ContainsAnyToken(label, "install", "download", "start", "get started", "try", "buy", "add to cart", "checkout", "contact sales", "sign up", "create account")) {
            score += 4;
        }
        if (label.Length <= 40) {
            score += 1;
        }
        if (ContainsAnyToken(label, "learn more", "read more", "home", "docs", "privacy")) {
            score -= 2;
        }
        if (element.Closest("nav, header, footer") != null) {
            score -= 2;
        }
        return score;
    }

    private static string DetectStructuredActionType(IElement element) {
        if (string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase)) {
            return "link";
        }
        if (string.Equals(element.LocalName, "button", StringComparison.OrdinalIgnoreCase)) {
            return "button";
        }

        return string.Equals(element.GetAttribute("type"), "submit", StringComparison.OrdinalIgnoreCase)
            ? "submit"
            : "button";
    }

    private static string DetectStructuredActionIntent(string label, IElement element) {
        if (ContainsAnyToken(label, "install")) {
            return "install";
        }
        if (ContainsAnyToken(label, "download")) {
            return "download";
        }
        if (ContainsAnyToken(label, "buy", "add to cart", "checkout")) {
            return "buy";
        }
        if (ContainsAnyToken(label, "start", "get started", "try")) {
            return "start";
        }
        if (ContainsAnyToken(label, "contact sales")) {
            return "contact-sales";
        }
        return string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase) ? "navigate" : "action";
    }

}
