using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting Open Graph metadata from HTML documents.
/// </summary>
public static class HtmlParserFromOpenGraph {
    /// <summary>
    /// Parses Open Graph metadata from HTML markup.
    /// </summary>
    /// <param name="html">HTML content containing Open Graph meta tags.</param>
    /// <returns>The parsed Open Graph metadata.</returns>
    /// <example>
    /// <code>
    /// var og = HtmlParserFromOpenGraph.ParseOpenGraph(html);
    /// string title = og.Properties["title"].First();
    /// </code>
    /// </example>
    public static HtmlOpenGraph ParseOpenGraph(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var nodes = document.QuerySelectorAll("meta[property^='og:']");
        HtmlOpenGraph result = new();
        foreach (var node in nodes) {
            string? property = node.GetAttribute("property");
            if (string.IsNullOrEmpty(property)) {
                continue;
            }
            string key = property.Substring(3); // remove "og:" prefix
            string content = node.GetAttribute("content") ?? string.Empty;
            if (!result.Properties.TryGetValue(key, out var list)) {
                list = new List<string>();
                result.Properties[key] = list;
            }
            if (!string.IsNullOrEmpty(content)) {
                list.Add(content);
            }
        }
        return result;
    }

    /// <summary>
    /// Downloads and parses Open Graph metadata from a URL.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>The parsed Open Graph metadata.</returns>
    public static async Task<HtmlOpenGraph> ParseUrlOpenGraphAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseOpenGraph(content);
    }
}
