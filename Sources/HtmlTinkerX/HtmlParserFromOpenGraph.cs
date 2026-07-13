using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting Open Graph metadata from HTML documents.
/// </summary>
public static partial class HtmlParserFromOpenGraph {
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
    public static HtmlOpenGraph ParseOpenGraph(string? html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var nodes = document.QuerySelectorAll("meta[property^='og:']");
        HtmlOpenGraph result = new();
        foreach (var node in nodes) {
            string? property = node.GetAttribute("property");
            if (property == null || property.Length == 0) {
                continue;
            }
            string propValue = property;
            string key = propValue.Substring(3); // remove "og:" prefix
            string content = node.GetAttribute("content") ?? string.Empty;

            OpenGraphProperty? existing = result.Properties.Find(p => p.Name == key);
            if (existing == null) {
                existing = new OpenGraphProperty { Name = key };
                result.Properties.Add(existing);
            }

            if (!string.IsNullOrEmpty(content)) {
                existing.Values.Add(content);
            }
        }
        return result;
    }

    /// <summary>
    /// Downloads and parses Open Graph metadata from a URL.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed Open Graph metadata.</returns>
    public static async Task<HtmlOpenGraph> ParseUrlOpenGraphAsync(string? url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        return ParseOpenGraph(content);
    }
}
