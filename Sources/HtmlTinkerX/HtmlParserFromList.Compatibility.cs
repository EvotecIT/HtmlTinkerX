using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlParserFromList {
    /// <summary>Preserves the 2.0.x binary signature for detailed AngleSharp URL list parsing.</summary>
    public static Task<List<HtmlListResult>> ParseUrlListsWithAngleSharpDetailedAsync(string? url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithAngleSharpDetailedAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for AngleSharp URL list parsing.</summary>
    public static Task<List<List<string>>> ParseUrlListsWithAngleSharpAsync(string? url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithAngleSharpAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for detailed HtmlAgilityPack URL list parsing.</summary>
    public static Task<List<HtmlListResult>> ParseUrlListsWithHtmlAgilityPackDetailedAsync(string? url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithHtmlAgilityPackDetailedAsync(url, tagPlaceholder, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for HtmlAgilityPack URL list parsing.</summary>
    public static Task<List<List<string>>> ParseUrlListsWithHtmlAgilityPackAsync(string? url, string tagPlaceholder, HttpClient? client) =>
        ParseUrlListsWithHtmlAgilityPackAsync(url, tagPlaceholder, client, null, default);
}
