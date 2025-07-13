using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for extracting &lt;meta&gt; tags from HTML.
/// </summary>
public static class HtmlParserFromMeta {
    /// <summary>
    /// Parses meta tags from HTML content.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <returns>List of meta tags.</returns>
    /// <example>
    /// <code>
    /// var tags = HtmlParserFromMeta.ParseMetaTags("<meta name='a' content='b'>");
    /// </code>
    /// </example>
    public static List<HtmlMetaTag> ParseMetaTags(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var nodes = document.QuerySelectorAll("meta");
        List<HtmlMetaTag> result = new();
        foreach (var node in nodes) {
            string name = node.GetAttribute("name") ?? node.GetAttribute("property") ?? string.Empty;
            string content = node.GetAttribute("content") ?? string.Empty;
            if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(content)) {
                result.Add(new HtmlMetaTag {
                    Name = name,
                    Content = content
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Downloads and parses meta tags from a URL.
    /// </summary>
    /// <param name="url">URL to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of meta tags.</returns>
    public static async Task<List<HtmlMetaTag>> ParseUrlMetaTagsAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseMetaTags(content);
    }
}
