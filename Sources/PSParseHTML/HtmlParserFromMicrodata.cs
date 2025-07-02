using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for extracting schema.org microdata items from HTML.
/// </summary>
public static class HtmlParserFromMicrodata {
    /// <summary>
    /// Parses microdata items from HTML markup.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>List of microdata items.</returns>
    public static List<HtmlMicrodataItem> ParseMicrodata(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var items = document.QuerySelectorAll("[itemscope]");
        List<HtmlMicrodataItem> results = new();
        foreach (var element in items) {
            HtmlMicrodataItem item = new() {
                Id = element.GetAttribute("itemid"),
                Type = element.GetAttribute("itemtype")
            };

            foreach (var prop in element.QuerySelectorAll("[itemprop]")) {
                if (!IsDirectChild(element, prop)) {
                    continue;
                }

                string name = prop.GetAttribute("itemprop") ?? string.Empty;
                string value = prop.GetAttribute("content") ?? prop.TextContent.Trim();

                if (!item.Properties.TryGetValue(name, out var list)) {
                    list = new List<string>();
                    item.Properties[name] = list;
                }
                list.Add(value);
            }

            results.Add(item);
        }
        return results;

        static bool IsDirectChild(IElement scope, IElement element) {
            for (var parent = element.ParentElement; parent != null && parent != scope; parent = parent.ParentElement) {
                if (parent.HasAttribute("itemscope")) {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Downloads a page and parses microdata items.
    /// </summary>
    /// <param name="url">URL to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of microdata items.</returns>
    public static async Task<List<HtmlMicrodataItem>> ParseUrlMicrodataAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseMicrodata(content);
    }
}
