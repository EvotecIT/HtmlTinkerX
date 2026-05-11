using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharpHtmlParser = AngleSharp.Html.Parser.HtmlParser;

namespace HtmlTinkerX;

/// <summary>
/// Parses common web discovery formats such as sitemaps, RSS and Atom feeds.
/// </summary>
public static class HtmlDiscoveryParser {
    /// <summary>
    /// Extracts anchor links from HTML together with link text and nearby parent context.
    /// </summary>
    public static IReadOnlyList<HtmlDiscoveredLink> ParseLinks(string html, Uri? baseUri = null, int maxContextLength = 300) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        AngleSharpHtmlParser parser = new();
        using AngleSharp.Html.Dom.IHtmlDocument document = parser.ParseDocument(html);
        return document.QuerySelectorAll("a[href]")
            .Select(anchor => {
                string href = anchor.GetAttribute("href") ?? string.Empty;
                string resolved = ResolveUrl(href, baseUri);
                string text = NormalizeWhitespace(anchor.TextContent);
                string title = NormalizeWhitespace(anchor.GetAttribute("title") ?? string.Empty);
                string context = ExtractCleanContext(anchor, text);
                if (context.Length > maxContextLength) {
                    context = context.Substring(0, maxContextLength);
                }

                bool isExternal = baseUri != null
                    && Uri.TryCreate(resolved, UriKind.Absolute, out Uri? resolvedUri)
                    && !string.Equals(resolvedUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);

                return new HtmlDiscoveredLink {
                    Url = resolved,
                    Href = href.Trim(),
                    Text = text,
                    Title = title,
                    Context = context,
                    IsExternal = isExternal
                };
            })
            .Where(static link => !string.IsNullOrWhiteSpace(link.Url))
            .ToArray();
    }

    /// <summary>
    /// Extracts URLs from a sitemap urlset or sitemap index document.
    /// </summary>
    public static IReadOnlyList<string> ParseSitemapUrls(string xml, Uri? baseUri = null) {
        if (xml == null) {
            throw new ArgumentNullException(nameof(xml));
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return document
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "loc", StringComparison.OrdinalIgnoreCase))
            .Select(element => ResolveUrl(element.Value, baseUri))
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Extracts normalized items from an RSS or Atom feed document.
    /// </summary>
    public static IReadOnlyList<HtmlSyndicationItem> ParseSyndicationItems(string xml, Uri? baseUri = null, string? sourceFeedUrl = null) {
        if (xml == null) {
            throw new ArgumentNullException(nameof(xml));
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement? root = document.Root;
        if (root == null) {
            return Array.Empty<HtmlSyndicationItem>();
        }

        if (string.Equals(root.Name.LocalName, "feed", StringComparison.OrdinalIgnoreCase)) {
            return ParseAtomItems(root, baseUri, sourceFeedUrl);
        }

        return ParseRssItems(document, baseUri, sourceFeedUrl);
    }

    private static IReadOnlyList<HtmlSyndicationItem> ParseRssItems(XDocument document, Uri? baseUri, string? sourceFeedUrl) {
        List<HtmlSyndicationItem> items = new();
        foreach (XElement item in document.Descendants().Where(static element => string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase))) {
            string title = ElementValue(item, "title");
            string link = ResolveUrl(ElementValue(item, "link"), baseUri);
            if (string.IsNullOrWhiteSpace(link)) {
                link = ResolveUrl(ElementValue(item, "guid"), baseUri);
            }

            items.Add(new HtmlSyndicationItem {
                Title = title,
                Url = link,
                Summary = FirstNonEmpty(ElementValue(item, "description"), ElementValue(item, "summary")),
                Published = TryParseDate(FirstNonEmpty(ElementValue(item, "pubDate"), ElementValue(item, "published"))),
                Updated = TryParseDate(ElementValue(item, "updated")),
                SourceFeedUrl = sourceFeedUrl
            });
        }

        return items;
    }

    private static IReadOnlyList<HtmlSyndicationItem> ParseAtomItems(XElement root, Uri? baseUri, string? sourceFeedUrl) {
        List<HtmlSyndicationItem> items = new();
        foreach (XElement entry in root.Elements().Where(static element => string.Equals(element.Name.LocalName, "entry", StringComparison.OrdinalIgnoreCase))) {
            string link = ResolveUrl(GetAtomLink(entry), baseUri);
            items.Add(new HtmlSyndicationItem {
                Title = ElementValue(entry, "title"),
                Url = link,
                Summary = FirstNonEmpty(ElementValue(entry, "summary"), ElementValue(entry, "content")),
                Published = TryParseDate(ElementValue(entry, "published")),
                Updated = TryParseDate(ElementValue(entry, "updated")),
                SourceFeedUrl = sourceFeedUrl
            });
        }

        return items;
    }

    private static string GetAtomLink(XElement entry) {
        XElement? alternate = entry.Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(static element => {
                XAttribute? rel = element.Attribute("rel");
                return rel == null || string.Equals(rel.Value, "alternate", StringComparison.OrdinalIgnoreCase);
            });

        return alternate?.Attribute("href")?.Value ?? string.Empty;
    }

    private static string ElementValue(XElement parent, string localName) {
        XElement? element = parent.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        return element?.Value?.Trim() ?? string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) {
        foreach (string value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveUrl(string value, Uri? baseUri) {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute)) {
            return absolute.AbsoluteUri;
        }

        if (baseUri != null && Uri.TryCreate(baseUri, trimmed, out Uri? resolved)) {
            return resolved.AbsoluteUri;
        }

        return trimmed;
    }

    private static DateTimeOffset? TryParseDate(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)) {
            return parsed;
        }

        return null;
    }

    private static string ExtractCleanContext(IElement anchor, string fallbackText) {
        IElement? source = anchor.ParentElement ?? anchor;
        IElement clone = (IElement)source.Clone(deep: true);
        foreach (IElement noise in clone.QuerySelectorAll("script,style,noscript,template,svg").ToArray()) {
            noise.Remove();
        }

        string context = NormalizeWhitespace(clone.TextContent);
        return string.IsNullOrWhiteSpace(context) ? fallbackText : context;
    }

    private static string NormalizeWhitespace(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Regex.Replace(value, "\\s+", " ").Trim();
}
