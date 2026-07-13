using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting microdata items from HTML documents.
/// </summary>
public static partial class HtmlParserFromMicrodata {
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
        return MicrodataParser.ExtractItems(document);
    }

    /// <summary>
    /// Downloads and parses microdata items from a URL.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of microdata items.</returns>
    public static async Task<List<HtmlMicrodataItem>> ParseUrlMicrodataItemsAsync(string url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        return ParseMicrodataItems(content);
    }

}
