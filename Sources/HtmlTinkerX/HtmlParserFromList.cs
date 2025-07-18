using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides functionality for parsing HTML lists.
/// </summary>
public static class HtmlParserFromList {
    /// <summary>
    /// Extracts list items from HTML using AngleSharp with metadata.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static List<HtmlListResult> ParseListsWithAngleSharpDetailed(string html, string tagPlaceholder = " ") {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var lists = document.QuerySelectorAll("ul,ol");
        List<HtmlListResult> results = new();

        for (int listIndex = 0; listIndex < lists.Length; listIndex++) {
            var list = lists[listIndex];
            var items = list.QuerySelectorAll("li");
            HtmlListResult result = new();
            var metadata = result.Metadata;
            metadata.ListIndex = listIndex;
            metadata.Id = list.Id;
            metadata.Classes = list.ClassName;
            metadata.IsOrdered = list.NodeName.Equals("ol", StringComparison.OrdinalIgnoreCase);
            foreach (var attr in list.Attributes) {
                metadata.Attributes[attr.Name] = attr.Value ?? string.Empty;
            }
            metadata.ItemCount = items.Length;
            var style = list.GetAttribute("style") ?? string.Empty;
            var containsDisplayNone = style.IndexOf("display:none", StringComparison.OrdinalIgnoreCase) >= 0;
            var containsDisplaySpaceNone = style.IndexOf("display: none", StringComparison.OrdinalIgnoreCase) >= 0;
            metadata.IsVisible = !(containsDisplayNone || containsDisplaySpaceNone);

            foreach (var item in items) {
                List<string> segments = new();
                CollectSegments(item, segments);
                if (segments.Count > 0) {
                    result.Items.Add(segments);
                }
            }

            if (result.Items.Count > 0) {
                results.Add(result);
            }
        }
        return results;

        static void CollectSegments(INode node, List<string> list) {
            if (node.NodeType == NodeType.Text) {
                string text = node.TextContent.Trim();
                if (!string.IsNullOrWhiteSpace(text)) {
                    list.Add(text);
                }
            } else {
                foreach (var child in node.ChildNodes) {
                    CollectSegments(child, list);
                }
            }
        }
    }

    /// <summary>
    /// Extracts list items from HTML using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with joined item texts.</returns>
    public static List<List<string>> ParseListsWithAngleSharp(string html, string tagPlaceholder = " ") {
        var detailed = ParseListsWithAngleSharpDetailed(html, tagPlaceholder);
        List<List<string>> result = new();
        foreach (var list in detailed) {
            result.Add(list.Items.Select(i => string.Join(tagPlaceholder, i)).ToList());
        }
        return result;
    }

    /// <summary>
    /// Extracts list items from a web page using AngleSharp with metadata.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    /// <param name="client">Optional HTTP client.</param>
    public static async Task<List<HtmlListResult>> ParseUrlListsWithAngleSharpDetailedAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseListsWithAngleSharpDetailed(content, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with joined item texts.</returns>
    /// <param name="client">Optional HTTP client.</param>
    public static async Task<List<List<string>>> ParseUrlListsWithAngleSharpAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        var detailed = await ParseUrlListsWithAngleSharpDetailedAsync(url, tagPlaceholder, client).ConfigureAwait(false);
        List<List<string>> result = new();
        foreach (var list in detailed) {
            result.Add(list.Items.Select(i => string.Join(tagPlaceholder, i)).ToList());
        }
        return result;
    }

    /// <summary>
    /// Extracts list items from HTML using HtmlAgilityPack with metadata.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static List<HtmlListResult> ParseListsWithHtmlAgilityPackDetailed(string html, string tagPlaceholder = " ") {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var nodes = doc.DocumentNode.SelectNodes("//ul|//ol");
        List<HtmlListResult> results = new();
        if (nodes == null) {
            return results;
        }

        int index = 0;
        foreach (var list in nodes) {
            var items = list.SelectNodes("li");
            if (items == null) {
                index++;
                continue;
            }

            HtmlListResult result = new();
            var metadata = result.Metadata;
            metadata.ListIndex = index++;
            metadata.Id = list.Id;
            metadata.Classes = list.GetAttributeValue("class", string.Empty);
            metadata.IsOrdered = list.Name.Equals("ol", StringComparison.OrdinalIgnoreCase);
            foreach (var attr in list.Attributes) {
                metadata.Attributes[attr.Name] = attr.Value ?? string.Empty;
            }
            metadata.ItemCount = items.Count;
            var style = list.GetAttributeValue("style", string.Empty);
            var containsDisplayNone = style.IndexOf("display:none", StringComparison.OrdinalIgnoreCase) >= 0;
            var containsDisplaySpaceNone = style.IndexOf("display: none", StringComparison.OrdinalIgnoreCase) >= 0;
            metadata.IsVisible = !(containsDisplayNone || containsDisplaySpaceNone);

            foreach (var item in items) {
                List<string> segments = new();
                CollectSegments(item, segments);
                if (segments.Count > 0) {
                    result.Items.Add(segments);
                }
            }

            if (result.Items.Count > 0) {
                results.Add(result);
            }
        }
        return results;

        static void CollectSegments(HtmlNode node, List<string> list) {
            if (node.NodeType == HtmlNodeType.Text) {
                string text = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text)) {
                    list.Add(text);
                }
            } else {
                foreach (var child in node.ChildNodes) {
                    CollectSegments(child, list);
                }
            }
        }
    }

    /// <summary>
    /// Extracts list items from HTML using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with joined item texts.</returns>
    public static List<List<string>> ParseListsWithHtmlAgilityPack(string html, string tagPlaceholder = " ") {
        var detailed = ParseListsWithHtmlAgilityPackDetailed(html, tagPlaceholder);
        List<List<string>> result = new();
        foreach (var list in detailed) {
            result.Add(list.Items.Select(i => string.Join(tagPlaceholder, i)).ToList());
        }
        return result;
    }

    /// <summary>
    /// Extracts list items from a web page using HtmlAgilityPack with metadata.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    /// <param name="client">Optional HTTP client.</param>
    public static async Task<List<HtmlListResult>> ParseUrlListsWithHtmlAgilityPackDetailedAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseListsWithHtmlAgilityPackDetailed(content, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with joined item texts.</returns>
    /// <param name="client">Optional HTTP client.</param>
    public static async Task<List<List<string>>> ParseUrlListsWithHtmlAgilityPackAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        var detailed = await ParseUrlListsWithHtmlAgilityPackDetailedAsync(url, tagPlaceholder, client).ConfigureAwait(false);
        List<List<string>> result = new();
        foreach (var list in detailed) {
            result.Add(list.Items.Select(i => string.Join(tagPlaceholder, i)).ToList());
        }
        return result;
    }
}