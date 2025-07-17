using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting microdata items from HTML documents.
/// </summary>
public static class HtmlParserFromMicrodata {
    /// <summary>
    /// Parses microdata items from HTML content.
    /// </summary>
    /// <param name="html">HTML markup containing microdata.</param>
    /// <returns>List of microdata items.</returns>
    /// <example>
    /// <code>
    /// var items = HtmlParserFromMicrodata.ParseMicrodataItems(html);
    /// </code>
    /// </example>
    public static List<HtmlMicrodataItem> ParseMicrodataItems(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return ExtractItems(document);
    }

    /// <summary>
    /// Downloads and parses microdata items from a URL.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of microdata items.</returns>
    public static async Task<List<HtmlMicrodataItem>> ParseUrlMicrodataItemsAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseMicrodataItems(content);
    }

    private static List<HtmlMicrodataItem> ExtractItems(IDocument document) {
        List<HtmlMicrodataItem> items = new();
        foreach (var root in document.QuerySelectorAll("[itemscope]:not([itemprop])")) {
            items.Add(ParseItem(root));
        }
        return items;
    }

    private static HtmlMicrodataItem ParseItem(IElement element) {
        HtmlMicrodataItem item = new() {
            Type = element.GetAttribute("itemtype"),
            Id = element.GetAttribute("itemid")
        };

        foreach (var prop in element.QuerySelectorAll("[itemprop]")) {
            if (!IsDirectChildOf(prop, element)) {
                continue;
            }
            string name = prop.GetAttribute("itemprop") ?? string.Empty;
            string value = GetPropertyValue(prop);
            if (!item.Properties.TryGetValue(name, out var list)) {
                list = new List<string>();
                item.Properties[name] = list;
            }
            if (!string.IsNullOrEmpty(value)) {
                list.Add(value);
            }
        }
        return item;
    }

    private static bool IsDirectChildOf(IElement element, IElement parent) {
        for (var node = element.ParentElement; node != null; node = node.ParentElement) {
            if (node == parent) {
                return true;
            }
            if (node.HasAttribute("itemscope") && !node.HasAttribute("itemprop")) {
                break;
            }
        }
        return false;
    }

    private static string GetPropertyValue(IElement element) {
        return element.GetAttribute("content")
            ?? element.GetAttribute("href")
            ?? element.GetAttribute("src")
            ?? element.GetAttribute("data")
            ?? element.GetAttribute("value")
            ?? element.TextContent.Trim();
    }
}
