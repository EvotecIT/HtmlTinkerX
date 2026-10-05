using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static readonly HashSet<string> CacheHeaderNames = new(StringComparer.OrdinalIgnoreCase) {
        "Accept", "Accept-Language", "Accept-Encoding", "User-Agent", "Cache-Control", "Pragma"
    };

    private sealed class CrawlHttpRequest(Uri uri, HtmlCrawlPage? cachedPage) : HttpRequestMessage(HttpMethod.Get, uri) {
        public HtmlCrawlPage? CachedPage { get; } = cachedPage;
        public Dictionary<string, string>? CacheRequestHeaders { get; set; }
        public bool CacheValidated { get; set; }
    }

    private static Dictionary<string, string>? GetCacheRequestHeaders(HttpRequestMessage request, string cookieHeader) {
        // Avoid storing credentials, custom tokens, or session-specific bodies in a reusable cache.
        if (cookieHeader.Length > 0 || request.Headers.CacheControl?.NoStore == true
            || request.Headers.Any(header => !CacheHeaderNames.Contains(header.Key))) return null;
        return request.Headers.ToDictionary(header => header.Key.ToLowerInvariant(),
            header => string.Join(", ", header.Value), StringComparer.Ordinal);
    }

    private static bool CanValidateCachedPage(HtmlCrawlPage? cached, Uri uri, HtmlCrawlOptions options,
        Dictionary<string, string>? headers) {
        HtmlCrawlHttpCacheEntry? entry = cached?.HttpCache;
        if (entry == null || headers == null || entry.RequestHeaders == null || entry.Html == null
            || cached!.Rendered || !string.Equals(cached.ResponseUrl, uri.GetLeftPart(UriPartial.Query), StringComparison.Ordinal)
            || entry.ByteLength < 0 || entry.ByteLength > options.MaximumPageResponseBytes
            || entry.Html.Length > options.MaximumPageResponseBytes
            || headers.Count != entry.RequestHeaders.Count
            || headers.Any(header => !entry.RequestHeaders.TryGetValue(header.Key, out string? value)
                || !string.Equals(value, header.Value, StringComparison.Ordinal))) return false;
        return (EntityTagHeaderValue.TryParse(cached.EntityTag, out EntityTagHeaderValue? tag) && tag.Tag != "*")
            || cached.LastModified.HasValue;
    }

    private static bool CanReuseValidatedResponse(CrawlHttpRequest request, HttpResponseMessage response) {
        if (!request.CacheValidated || response.StatusCode != HttpStatusCode.NotModified) return false;
        if (!response.Headers.TryGetValues("ETag", out IEnumerable<string>? values)) return true;
        if (!EntityTagHeaderValue.TryParse(string.Join(", ", values), out EntityTagHeaderValue? responseTag)
            || responseTag.Tag == "*") return false;
        return !EntityTagHeaderValue.TryParse(request.CachedPage?.EntityTag, out EntityTagHeaderValue? storedTag)
            || string.Equals(storedTag.Tag, responseTag.Tag, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendPageRequestAsync(HttpClient client, CrawlHttpRequest request,
        CancellationToken token) {
        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (request.CacheValidated && response.StatusCode == HttpStatusCode.NotModified && !CanReuseValidatedResponse(request, response)) {
            response.Dispose();
            // A mismatched validator cannot authenticate the stored body. Retry once without our condition.
            using CrawlHttpRequest unconditional = new(request.RequestUri!, null);
            response = await client.SendAsync(unconditional, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            request.CacheValidated = false;
            request.CacheRequestHeaders = unconditional.CacheRequestHeaders;
        }
        return response;
    }

    private static bool CanStoreResponse(HttpResponseMessage response, Dictionary<string, string>? requestHeaders) =>
        requestHeaders != null && !response.Headers.Contains("Set-Cookie")
        && (!response.Headers.Contains("Cache-Control") || response.Headers.CacheControl != null)
        && response.Headers.CacheControl?.NoStore != true
        && (!response.Headers.TryGetValues("Vary", out IEnumerable<string>? vary)
            || vary.SelectMany(value => value.Split(',')).All(name => CacheHeaderNames.Contains(name.Trim())));

    private static string ComputeResponseHash(byte[] bytes) {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static async Task<Dictionary<string, HtmlCrawlPage>> LoadRefreshPagesAsync(Uri startUri,
        HtmlCrawlOptions options, CancellationToken token) {
        Dictionary<string, HtmlCrawlPage> pages = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(options.RefreshPath)) return pages;
        HtmlCrawlResult previous = await LoadResultAsync(options.RefreshPath!, token).ConfigureAwait(false);
        if (!string.Equals(previous.StartUrl, startUri.AbsoluteUri, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Refresh data was created for '{previous.StartUrl}', but the current crawl starts from '{startUri.AbsoluteUri}'.");
        }
        foreach (HtmlCrawlPage page in previous.Pages.Concat(previous.SkippedPages)) {
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(page.RequestedUrl)) pages[page.RequestedUrl!] = page;
        }
        return pages;
    }
}
