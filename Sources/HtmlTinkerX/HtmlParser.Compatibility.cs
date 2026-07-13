using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlParser {
    /// <summary>Preserves the 2.0.x binary signature for AngleSharp URL parsing.</summary>
    public static Task<IDocument> ParseUrlWithAngleSharpAsync(string url, HttpClient? client, CancellationToken cancellationToken) =>
        ParseUrlWithAngleSharpAsync(url, client, null, cancellationToken);

    /// <summary>Preserves the 2.0.x binary signature for HtmlAgilityPack URL parsing.</summary>
    public static Task<HtmlDocument> ParseUrlWithHtmlAgilityPackAsync(string url, HttpClient? client, CancellationToken cancellationToken) =>
        ParseUrlWithHtmlAgilityPackAsync(url, client, null, cancellationToken);

    /// <summary>Preserves the 2.0.x binary signature for AngleSharp URL table parsing.</summary>
    public static Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent,
        IDictionary<string, string>? replaceHeaders,
        bool allProperties,
        HttpClient? client,
        Func<HttpClient>? clientFactory) =>
        ParseUrlTablesWithAngleSharpAsync(url, replaceContent, replaceHeaders, allProperties, client, clientFactory, null, default);

    /// <summary>Preserves the 2.0.x binary signature for HtmlAgilityPack URL table parsing.</summary>
    public static Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithHtmlAgilityPackAsync(
        string url,
        bool reverseTable,
        IDictionary<string, string>? replaceContent,
        IDictionary<string, string>? replaceHeaders,
        bool allProperties,
        HttpClient? client,
        Func<HttpClient>? clientFactory) =>
        ParseUrlTablesWithHtmlAgilityPackAsync(url, reverseTable, replaceContent, replaceHeaders, allProperties, client, clientFactory, null, default);

    /// <summary>Preserves the 2.0.x binary signature for AngleSharp URL list parsing.</summary>
    public static Task<List<List<string>>> ParseUrlListsWithAngleSharpAsync(string url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithAngleSharpAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for HtmlAgilityPack URL list parsing.</summary>
    public static Task<List<List<string>>> ParseUrlListsWithHtmlAgilityPackAsync(string url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithHtmlAgilityPackAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for detailed AngleSharp URL list parsing.</summary>
    public static Task<List<HtmlListResult>> ParseUrlListsWithAngleSharpDetailedAsync(string url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithAngleSharpDetailedAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for detailed HtmlAgilityPack URL list parsing.</summary>
    public static Task<List<HtmlListResult>> ParseUrlListsWithHtmlAgilityPackDetailedAsync(string url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithHtmlAgilityPackDetailedAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for URL form parsing.</summary>
    public static Task<List<HtmlFormResult>> ParseUrlFormsWithAngleSharpAsync(string url, HttpClient? client) =>
        ParseUrlFormsWithAngleSharpAsync(url, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for URL metadata parsing.</summary>
    public static Task<List<HtmlMetaTag>> ParseUrlMetaTagsAsync(string url, HttpClient? client) =>
        ParseUrlMetaTagsAsync(url, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for URL Open Graph parsing.</summary>
    public static Task<HtmlOpenGraph> ParseUrlOpenGraphAsync(string url, HttpClient? client) =>
        ParseUrlOpenGraphAsync(url, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for URL microdata parsing.</summary>
    public static Task<List<HtmlMicrodataItem>> ParseUrlMicrodataItemsAsync(string url, HttpClient? client) =>
        ParseUrlMicrodataItemsAsync(url, client, null, default);
}
