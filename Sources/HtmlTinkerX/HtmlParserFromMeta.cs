using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

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
    /// var tags = HtmlParserFromMeta.ParseMetaTags("&lt;meta name='a' content='b'&gt;");
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
            string sourceAttribute = GetSourceAttribute(node);
            string name = sourceAttribute.Length > 0 ? node.GetAttribute(sourceAttribute) ?? string.Empty : string.Empty;
            string content = node.GetAttribute("content") ?? string.Empty;
            if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(content)) {
                result.Add(new HtmlMetaTag {
                    Name = name,
                    SourceAttribute = sourceAttribute,
                    Content = content
                });
            }
        }
        return result;
    }

    private static string GetSourceAttribute(IElement node) {
        if (node.HasAttribute("name")) return "name";
        if (node.HasAttribute("property")) return "property";
        if (node.HasAttribute("itemprop")) return "itemprop";
        if (node.HasAttribute("http-equiv")) return "http-equiv";
        return string.Empty;
    }

    /// <summary>
    /// Downloads and parses meta tags from a URL.
    /// </summary>
    /// <param name="url">URL to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of meta tags.</returns>
    public static async Task<List<HtmlMetaTag>> ParseUrlMetaTagsAsync(string url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        return ParseMetaTags(content);
    }
}
