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
        return ParseLinksDocument(document, baseUri, maxContextLength);
    }

    internal static IReadOnlyList<HtmlDiscoveredLink> ParseLinksDocument(IDocument document, Uri? baseUri = null, int maxContextLength = 300) {
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
                    && !HtmlUriUtility.HasSameOrigin(baseUri, resolvedUri);

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
    public static IReadOnlyList<HtmlSyndicationItem> ParseSyndicationItems(string xml, Uri? baseUri = null, string? sourceFeedUrl = null) =>
        ParseSyndicationFeed(xml, baseUri, sourceFeedUrl).Items;

    /// <summary>
    /// Extracts an RSS or Atom feed document: its channel-level title, link and last-updated time, and its normalized items.
    /// </summary>
    public static HtmlSyndicationFeed ParseSyndicationFeed(string xml, Uri? baseUri = null, string? sourceFeedUrl = null) {
        if (xml == null) {
            throw new ArgumentNullException(nameof(xml));
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement? root = document.Root;
        if (root == null) {
            return new HtmlSyndicationFeed { SourceFeedUrl = sourceFeedUrl };
        }

        if (string.Equals(root.Name.LocalName, "feed", StringComparison.OrdinalIgnoreCase)) {
            return new HtmlSyndicationFeed {
                Title = ElementValue(root, "title"),
                Url = ResolveUrl(GetAtomLink(root), baseUri),
                // Atom 0.3 feeds use modified.
                Updated = TryParseDate(FirstNonEmpty(ElementValue(root, "updated"), ElementValue(root, "modified"))),
                SourceFeedUrl = sourceFeedUrl,
                Items = ParseAtomItems(root, baseUri, sourceFeedUrl)
            };
        }

        XElement? channel = document.Descendants().FirstOrDefault(static element => string.Equals(element.Name.LocalName, "channel", StringComparison.OrdinalIgnoreCase));
        return new HtmlSyndicationFeed {
            Title = channel == null ? string.Empty : ElementValue(channel, channel.Name.Namespace, "title"),
            // The channel's own link, not an atom:link rel="self" that many RSS 2.0 feeds also carry.
            Url = channel == null ? string.Empty : ResolveUrl(ElementValue(channel, channel.Name.Namespace, "link"), baseUri),
            Updated = channel == null ? null : TryParseDate(FirstNonEmpty(ElementValue(channel, "lastBuildDate"), ElementValue(channel, "pubDate"),
                ElementValue(channel, DublinCore, "date"))),
            SourceFeedUrl = sourceFeedUrl,
            Items = ParseRssItems(document, baseUri, sourceFeedUrl)
        };
    }

    private static IReadOnlyList<HtmlSyndicationItem> ParseRssItems(XDocument document, Uri? baseUri, string? sourceFeedUrl) {
        List<HtmlSyndicationItem> items = new();
        foreach (XElement item in document.Descendants().Where(static element => string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase))) {
            string title = ElementValue(item, "title");
            string link = ResolveUrl(ElementValue(item, "link"), baseUri);
            if (string.IsNullOrWhiteSpace(link)) {
                link = ResolveUrl(ElementValue(item, "guid"), baseUri);
            }

            XElement? guid = FirstChild(item, "guid");
            string? id = EmptyToNull(guid?.Value);
            items.Add(new HtmlSyndicationItem {
                Title = title,
                Url = link,
                Summary = FirstNonEmpty(ElementValue(item, "description"), ElementValue(item, "summary")),
                // Many feeds (WordPress, RSS 1.0/RDF, Jamf) date items only with Dublin Core: dc:date, dcterms:created, dcterms:modified.
                Published = TryParseDate(FirstNonEmpty(ElementValue(item, "pubDate"), ElementValue(item, "published"),
                    ElementValue(item, DublinCore, "date"), ElementValue(item, DublinCoreTerms, "created"), ElementValue(item, DublinCoreTerms, "date"))),
                Updated = TryParseDate(FirstNonEmpty(ElementValue(item, "updated"), ElementValue(item, DublinCoreTerms, "modified"))),
                SourceFeedUrl = sourceFeedUrl,
                Id = id,
                // RSS 2.0: a guid is a permalink unless isPermaLink="false".
                IdIsPermaLink = id != null && !string.Equals(guid!.Attribute("isPermaLink")?.Value?.Trim(), "false", StringComparison.OrdinalIgnoreCase),
                Categories = ParseCategories(item),
                Content = EmptyToNull(ElementValue(item, ContentModule, "encoded"))
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
                // Atom 0.3 feeds use issued and modified.
                Published = TryParseDate(FirstNonEmpty(ElementValue(entry, "published"), ElementValue(entry, "issued"), ElementValue(entry, DublinCore, "date"))),
                Updated = TryParseDate(FirstNonEmpty(ElementValue(entry, "updated"), ElementValue(entry, "modified"))),
                SourceFeedUrl = sourceFeedUrl,
                Id = EmptyToNull(ElementValue(entry, "id")),
                IdIsPermaLink = false,
                Categories = ParseCategories(entry),
                Content = AtomContent(FirstChild(entry, "content"))
            });
        }

        return items;
    }

    /// <summary>RSS category text or the Atom category term (label when there is no term), trimmed, in order, without duplicates.</summary>
    private static IReadOnlyList<string> ParseCategories(XElement parent) {
        List<string> categories = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement category in parent.Elements().Where(static element => string.Equals(element.Name.LocalName, "category", StringComparison.OrdinalIgnoreCase))) {
            string value = FirstNonEmpty(category.Value, category.Attribute("term")?.Value ?? string.Empty, category.Attribute("label")?.Value ?? string.Empty);
            if (value.Length > 0 && seen.Add(value)) {
                categories.Add(value);
            }
        }

        return categories.Count == 0 ? Array.Empty<string>() : categories.ToArray();
    }

    /// <summary>The Atom content as the feed wrote it: text or HTML (unescaped once by XML), or the inner markup of an xhtml div.</summary>
    private static string? AtomContent(XElement? content) {
        if (content == null) {
            return null;
        }

        if (string.Equals(content.Attribute("type")?.Value?.Trim(), "xhtml", StringComparison.OrdinalIgnoreCase)) {
            XElement? wrapper = content.Elements().FirstOrDefault(static element => string.Equals(element.Name.LocalName, "div", StringComparison.OrdinalIgnoreCase));
            IEnumerable<XNode> nodes = wrapper?.Nodes() ?? content.Nodes();
            return EmptyToNull(string.Concat(nodes.Select(static node => node.ToString(SaveOptions.DisableFormatting))));
        }

        return EmptyToNull(content.Value);
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

    private static readonly XNamespace DublinCore = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DublinCoreTerms = "http://purl.org/dc/terms/";
    private static readonly XNamespace ContentModule = "http://purl.org/rss/1.0/modules/content/";

    /// <summary>The trimmed value of the first child with this exact namespace and local name, or empty.</summary>
    private static string ElementValue(XElement parent, XNamespace ns, string localName) =>
        parent.Element(ns + localName)?.Value?.Trim() ?? string.Empty;

    private static string ElementValue(XElement parent, string localName) =>
        FirstChild(parent, localName)?.Value?.Trim() ?? string.Empty;

    private static XElement? FirstChild(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

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

        bool isRootRelativeReference = trimmed[0] == '/' || trimmed[0] == '\\';
        if (baseUri != null && (isRootRelativeReference || !Uri.TryCreate(trimmed, UriKind.Absolute, out _))) {
            string normalized = trimmed.Replace('\\', '/');
            if (Uri.TryCreate(baseUri, normalized, out Uri? resolved)) {
                return resolved.AbsoluteUri;
            }
        }

        if (isRootRelativeReference) {
            return trimmed.Replace('\\', '/');
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute)) {
            return absolute.AbsoluteUri;
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

        // RFC 822 zone names (PDT, UTC) and a wrong weekday defeat the general parser.
        if (HtmlDateParser.TryParse(value, out parsed)) {
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
