using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static HttpClient CreateClient(HtmlCrawlOptions options, Uri startUri) {
        NetworkCredential? proxyCredential = string.IsNullOrEmpty(options.ProxyUsername) && string.IsNullOrEmpty(options.ProxyPassword)
            ? null : new NetworkCredential(options.ProxyUsername, options.ProxyPassword);
        HttpClient transport = HtmlHttpClientFactory.Create(options.Proxy, proxyCredential, allowAutoRedirect: false);
        Dictionary<string, string> headers = transport.DefaultRequestHeaders.ToDictionary(
            header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        transport.DefaultRequestHeaders.Clear();
        transport.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        foreach (var header in options.Headers) headers[header.Key] = header.Value;
        if (!string.IsNullOrEmpty(options.UserAgent)) headers["User-Agent"] = options.UserAgent!;
        if (!string.IsNullOrEmpty(options.Username) && options.Password != null) {
            headers["Authorization"] = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"))).ToString();
        }
        return new HttpClient(new CrawlHttpHandler(transport, startUri, options, headers)) {
            Timeout = TimeSpan.FromMilliseconds(options.Timeout)
        };
    }

    private sealed class CrawlHttpHandler(
        HttpClient transport, Uri startUri, HtmlCrawlOptions options,
        IReadOnlyDictionary<string, string> scopedHeaders) : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Uri uri = request.RequestUri!;
            for (int hop = 0; ; hop++) {
                if (!IsCrawlHostAllowed(uri, startUri, options)) {
                    throw new HttpRequestException($"Request destination '{uri}' is outside the crawl scope.");
                }
                using HttpRequestMessage outgoing = new(request.Method, uri);
                if (HtmlUriUtility.HasSameOrigin(startUri, uri)) {
                    foreach (var header in scopedHeaders) outgoing.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    foreach (var header in request.Headers) {
                        outgoing.Headers.Remove(header.Key);
                        outgoing.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                HttpResponseMessage response = await transport.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                int status = (int)response.StatusCode;
                Uri? location = response.Headers.Location;
                if (location == null || !(status == 301 || status == 302 || status == 303 || status == 307 || status == 308)) {
                    return response;
                }
                response.Dispose();
                if (hop >= 49) throw new HttpRequestException("The crawl request exceeded 50 redirects.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) transport.Dispose();
            base.Dispose(disposing);
        }
    }

    private static bool IsCrawlHostAllowed(Uri uri, Uri startUri, HtmlCrawlOptions options) =>
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && (!options.RestrictToHost || IsHostInScope(uri.Host, startUri.Host, options.IncludeSubdomains));
}
