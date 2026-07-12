using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlResourceParser {
    /// <summary>Preserves the 2.0.x binary signature for URL resource parsing.</summary>
    public static Task<List<HtmlResourceLink>> ParseUrlAsync(string url, bool includeCss, bool includeInline, HttpClient? client) =>
        ParseUrlAsync(url, includeCss, includeInline, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for resource downloads.</summary>
    public static Task<List<string>> DownloadResourcesAsync(IEnumerable<HtmlResourceLink> links, Uri baseUri, string directory, HttpClient? client) =>
        DownloadResourcesAsync(links, baseUri, directory, client, null, default);

    /// <summary>Preserves the 2.0.x binary signature for URL resource downloads.</summary>
    public static Task<List<string>> DownloadResourcesFromUrlAsync(string url, string directory, bool includeCss, HttpClient? client) =>
        DownloadResourcesFromUrlAsync(url, directory, includeCss, client, null, default);
}
