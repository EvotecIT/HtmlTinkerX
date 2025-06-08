using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML;

/// <summary>
/// Provides functionality for parsing HTML lists.
/// </summary>
public static class HtmlListParser {
    /// <summary>
    /// Extracts list items from HTML using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <returns>List of lists with item texts.</returns>
    public static List<List<string>> ParseListsWithAngleSharp(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var lists = document.QuerySelectorAll("ul,ol");
        List<List<string>> result = new();

        foreach (var list in lists) {
            var items = list.QuerySelectorAll("li");
            List<string> entry = items.Select(i => i.TextContent.Trim()).ToList();
            if (entry.Count > 0) {
                result.Add(entry);
            }
        }
        return result;
    }

    /// <summary>
    /// Extracts list items from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>List of lists with item texts.</returns>
    public static async Task<List<List<string>>> ParseUrlListsWithAngleSharpAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? new HttpClient();
        string content = await HttpContentHelper.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseListsWithAngleSharp(content);
    }

    /// <summary>
    /// Extracts list items from HTML using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <returns>List of lists with item texts.</returns>
    public static List<List<string>> ParseListsWithHtmlAgilityPack(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var nodes = doc.DocumentNode.SelectNodes("//ul|//ol");
        List<List<string>> result = new();

        if (nodes == null) {
            return result;
        }

        foreach (var list in nodes) {
            var items = list.SelectNodes("li");
            if (items == null) {
                continue;
            }
            List<string> entry = items.Select(i => HtmlEntity.DeEntitize(i.InnerText).Trim()).ToList();
            if (entry.Count > 0) {
                result.Add(entry);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts list items from a web page using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>List of lists with item texts.</returns>
    public static async Task<List<List<string>>> ParseUrlListsWithHtmlAgilityPackAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? new HttpClient();
        string content = await HttpContentHelper.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseListsWithHtmlAgilityPack(content);
    }
}
