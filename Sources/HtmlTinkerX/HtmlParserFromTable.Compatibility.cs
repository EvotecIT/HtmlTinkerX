using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlParserFromTable {
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
}
