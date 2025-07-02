using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Provides functionality for extracting <meta> tags from HTML.
/// </summary>
public static class HtmlParserFromMeta {
    /// <summary>
    /// Parses HTML markup and returns meta tag name/content pairs using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing meta tags.</param>
    /// <returns>List of meta tag objects.</returns>
    public static List<HtmlMetaTag> ParseMetaTagsWithAngleSharp(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var metas = document.QuerySelectorAll("meta");
        List<HtmlMetaTag> results = new();
        foreach (var meta in metas) {
            string? name = meta.GetAttribute("name") ??
                            meta.GetAttribute("property") ??
                            meta.GetAttribute("http-equiv") ??
                            (meta.HasAttribute("charset") ? "charset" : null);
            if (string.IsNullOrEmpty(name)) {
                continue;
            }
            string content = meta.GetAttribute("content") ??
                             meta.GetAttribute("charset") ?? string.Empty;
            results.Add(new HtmlMetaTag {
                Name = name!,
                Content = content
            });
        }
        return results;
    }

    /// <summary>
    /// Downloads HTML from a URL and parses meta tags using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of meta tag objects.</returns>
    public static async Task<List<HtmlMetaTag>> ParseUrlMetaTagsWithAngleSharpAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseMetaTagsWithAngleSharp(content);
    }

    /// <summary>
    /// Parses HTML markup and returns meta tag name/content pairs using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing meta tags.</param>
    /// <returns>List of meta tag objects.</returns>
    public static List<HtmlMetaTag> ParseMetaTagsWithHtmlAgilityPack(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var metas = doc.DocumentNode.SelectNodes("//meta");
        List<HtmlMetaTag> results = new();
        if (metas == null) {
            return results;
        }
        foreach (var meta in metas) {
            string name = meta.GetAttributeValue("name",
                meta.GetAttributeValue("property",
                    meta.GetAttributeValue("http-equiv",
                        meta.Attributes.Contains("charset") ? "charset" : string.Empty)));
            if (string.IsNullOrEmpty(name)) {
                continue;
            }
            string content = meta.GetAttributeValue("content",
                meta.GetAttributeValue("charset", string.Empty));
            results.Add(new HtmlMetaTag {
                Name = name,
                Content = content
            });
        }
        return results;
    }

    /// <summary>
    /// Downloads HTML from a URL and parses meta tags using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of meta tag objects.</returns>
    public static async Task<List<HtmlMetaTag>> ParseUrlMetaTagsWithHtmlAgilityPackAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseMetaTagsWithHtmlAgilityPack(content);
    }
}
