using System;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for parsing HTML content using either AngleSharp or HtmlAgilityPack.
/// </summary>
public static class HtmlParser {
    private static readonly HttpClient _client = new();

    /// <summary>
    /// Parses HTML markup from a string using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>The parsed <see cref="IDocument"/>.</returns>
    public static IDocument ParseWithAngleSharp(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        var parser = new global::AngleSharp.Html.Parser.HtmlParser();
        return parser.ParseDocument(html);
    }

    /// <summary>
    /// Downloads and parses HTML markup from a URL using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>The parsed <see cref="IDocument"/>.</returns>
    public static async Task<IDocument> ParseUrlWithAngleSharpAsync(string url) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseWithAngleSharp(content);
    }

    /// <summary>
    /// Parses HTML markup from a string using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>The parsed <see cref="HtmlDocument"/>.</returns>
    public static HtmlDocument ParseWithHtmlAgilityPack(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>
    /// Downloads and parses HTML markup from a URL using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>The parsed <see cref="HtmlDocument"/>.</returns>
    public static async Task<HtmlDocument> ParseUrlWithHtmlAgilityPackAsync(string url) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseWithHtmlAgilityPack(content);
    }
}
