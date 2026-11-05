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
    private static readonly TimeSpan HttpRetryBaseDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan HttpRetryMaximumDelay = TimeSpan.FromSeconds(30);

    private static HttpClient CreateClient(HtmlCrawlOptions options, Uri startUri) {
        NetworkCredential? proxyCredential = string.IsNullOrEmpty(options.ProxyUsername) && string.IsNullOrEmpty(options.ProxyPassword)
            ? null : new NetworkCredential(options.ProxyUsername, options.ProxyPassword);
        HttpClient transport = HtmlHttpClientFactory.Create(out CookieContainer cookies, options.Proxy, proxyCredential, allowAutoRedirect: false);
        Dictionary<string, string> headers = transport.DefaultRequestHeaders.ToDictionary(
            header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        transport.DefaultRequestHeaders.Clear();
        transport.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        foreach (var header in options.Headers) headers[header.Key] = header.Value;
        if (!string.IsNullOrEmpty(options.UserAgent)) headers["User-Agent"] = options.UserAgent!;
        headers.TryGetValue("User-Agent", out string? userAgent);
        headers.Remove("User-Agent");
        if (!string.IsNullOrEmpty(options.Username) && options.Password != null) {
            headers["Authorization"] = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"))).ToString();
        }
        return new HttpClient(new CrawlHttpHandler(transport, startUri, options, headers, userAgent, cookies)) {
            Timeout = TimeSpan.FromMilliseconds(options.Timeout)
        };
    }

    private sealed class CrawlHttpHandler(
        HttpClient transport, Uri startUri, HtmlCrawlOptions options,
        IReadOnlyDictionary<string, string> scopedHeaders, string? userAgent, CookieContainer cookies) : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Uri uri = request.RequestUri!;
            int redirects = 0;
            int retries = 0;
            while (true) {
                if (!IsCrawlHostAllowed(uri, startUri, options)) {
                    throw new HttpRequestException($"Request destination '{uri}' is outside the crawl scope.");
                }
                using HttpRequestMessage outgoing = new(request.Method, uri);
                if (!string.IsNullOrEmpty(userAgent)) outgoing.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                if (HtmlUriUtility.HasSameOrigin(startUri, uri)) {
                    foreach (var header in scopedHeaders) outgoing.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    foreach (var header in request.Headers) {
                        outgoing.Headers.Remove(header.Key);
                        outgoing.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                if (request is CrawlHttpRequest pageRequest) {
                    pageRequest.CacheValidated = false;
                    pageRequest.CacheRequestHeaders = GetCacheRequestHeaders(outgoing, cookies.GetCookieHeader(uri));
                    if (CanValidateCachedPage(pageRequest.CachedPage, uri, options, pageRequest.CacheRequestHeaders)) {
                        HtmlCrawlPage cached = pageRequest.CachedPage!;
                        pageRequest.CacheValidated = HtmlFetchRules.ApplyValidators(outgoing, cached.EntityTag, cached.LastModified);
                    }
                }
                HtmlHostRequestGate? gate = request is CrawlHttpRequest ? options.PageRequestGate : null;
                IDisposable? originLease = null;
                if (gate != null) {
                    int spacing = options.PageRequestDelay == null ? 0 : await options.PageRequestDelay(uri, cancellationToken).ConfigureAwait(false);
                    originLease = await gate.EnterAsync(uri, TimeSpan.FromMilliseconds(spacing), cancellationToken).ConfigureAwait(false);
                }
                HttpResponseMessage response;
                try {
                    response = await transport.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    if (originLease != null) {
                        response.Content = new HtmlLeasedContent(response.Content, originLease);
                        originLease = null;
                    }
                } finally {
                    originLease?.Dispose();
                }
                int status = (int)response.StatusCode;
                if (options.HttpRetryCount > 0 && HtmlFetchRules.IsTransientStatus(status)) {
                    // A terminal response can still ask other queued pages to wait.
                    gate?.Defer(uri, HtmlFetchRules.GetServerRetryDelay(response) ?? TimeSpan.Zero);
                }
                if (request.Method == HttpMethod.Get && retries < options.HttpRetryCount && HtmlFetchRules.IsTransientStatus(status)) {
                    TimeSpan delay = HtmlFetchRules.GetRetryDelay(response, retries, HttpRetryBaseDelay, HttpRetryMaximumDelay);
                    gate?.Defer(uri, delay);
                    // Keep the failure when the server's minimum wait exceeds the whole request budget.
                    if (delay.TotalMilliseconds >= options.Timeout) return response;
                    response.Dispose();
                    await HtmlFetchRules.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    retries++;
                    continue;
                }
                if (!HtmlFetchRules.TryGetRedirectTarget(response, uri, out Uri? target)) {
                    return response;
                }
                TimeSpan redirectDelay = options.HttpRetryCount > 0 ? HtmlFetchRules.GetServerRetryDelay(response) ?? TimeSpan.Zero : TimeSpan.Zero;
                gate?.Defer(uri, redirectDelay);
                if (redirectDelay.TotalMilliseconds >= options.Timeout) return response;
                response.Dispose();
                if (redirects++ >= 49) throw new HttpRequestException("The crawl request exceeded 50 redirects.");
                if (redirectDelay > TimeSpan.Zero) await HtmlFetchRules.WaitAsync(redirectDelay, cancellationToken).ConfigureAwait(false);
                uri = target!;
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
