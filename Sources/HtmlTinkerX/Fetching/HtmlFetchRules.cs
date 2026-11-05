using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Retry, redirect and revalidation rules shared by <see cref="HtmlFetchHandler"/> and the crawler, so both read the
/// web the same way.
/// </summary>
internal static class HtmlFetchRules {
    internal static readonly HashSet<string> CacheHeaderNames = new(StringComparer.OrdinalIgnoreCase) {
        "Accept", "Accept-Language", "Accept-Encoding", "User-Agent", "Cache-Control", "Pragma"
    };

    /// <summary>Statuses a busy site returns transiently. 403 is not among them: a refusal is classified instead.</summary>
    internal static bool IsTransientStatus(int status) => status == 408 || status == 429
        || status == 500 || status == 502 || status == 503 || status == 504;

    internal static bool IsIdempotent(HttpMethod method) => method == HttpMethod.Get || method == HttpMethod.Head;

    /// <summary>The server's Retry-After as a delay, or null when absent or invalid. A past date is zero.</summary>
    internal static TimeSpan? GetServerRetryDelay(HttpResponseMessage response) {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta) return delta;
        if (retryAfter?.Date is DateTimeOffset date) {
            TimeSpan remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
        return null;
    }

    /// <summary>The server's Retry-After, otherwise exponential backoff from <paramref name="baseDelay"/> capped at <paramref name="maximumDelay"/>.</summary>
    internal static TimeSpan GetRetryDelay(HttpResponseMessage? response, int retries, TimeSpan baseDelay, TimeSpan maximumDelay) {
        if (response != null && GetServerRetryDelay(response) is TimeSpan server) return server;
        double milliseconds = Math.Min(maximumDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, retries));
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    internal static async Task WaitAsync(TimeSpan delay, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan remaining = delay;
        while (remaining > TimeSpan.Zero) {
            // Timers can wake slightly early. Keep the minimum delay before another request.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds)), token).ConfigureAwait(false);
            remaining = delay - elapsed.Elapsed;
        }
    }

    internal static bool IsRedirectStatus(int status) =>
        status == 301 || status == 302 || status == 303 || status == 307 || status == 308;

    /// <summary>Resolves a redirect's Location against the current address.</summary>
    internal static bool TryGetRedirectTarget(HttpResponseMessage response, Uri current, out Uri? target) {
        target = null;
        if (!IsRedirectStatus((int)response.StatusCode) || response.Headers.Location is not Uri location) return false;
        target = location.IsAbsoluteUri ? location : new Uri(current, location);
        return true;
    }

    /// <summary>Adds a stored entity tag, or the stored Last-Modified date when no usable tag exists.</summary>
    internal static bool ApplyValidators(HttpRequestMessage request, string? entityTag, DateTimeOffset? lastModified) {
        if (EntityTagHeaderValue.TryParse(entityTag, out EntityTagHeaderValue? tag) && tag.Tag != "*") {
            request.Headers.IfNoneMatch.Add(tag);
            return true;
        }
        if (lastModified == null) return false;
        request.Headers.IfModifiedSince = lastModified;
        return true;
    }

    /// <summary>Whether a 304 authenticates the stored representation; a mismatched entity tag does not.</summary>
    internal static bool CanReuseNotModified(string? storedEntityTag, HttpResponseMessage response) {
        if (response.StatusCode != HttpStatusCode.NotModified) return false;
        if (!response.Headers.TryGetValues("ETag", out IEnumerable<string>? values)) return true;
        if (!EntityTagHeaderValue.TryParse(string.Join(", ", values), out EntityTagHeaderValue? responseTag)
            || responseTag.Tag == "*") return false;
        return !EntityTagHeaderValue.TryParse(storedEntityTag, out EntityTagHeaderValue? storedTag)
            || string.Equals(storedTag.Tag, responseTag.Tag, StringComparison.Ordinal);
    }

    /// <summary>Whether a response is safe to keep for revalidation: no session cookie, no-store or unknown Vary.</summary>
    internal static bool CanStoreResponse(HttpResponseMessage response) =>
        !response.Headers.Contains("Set-Cookie")
        && (!response.Headers.Contains("Cache-Control") || response.Headers.CacheControl != null)
        && response.Headers.CacheControl?.NoStore != true
        && (!response.Headers.TryGetValues("Vary", out IEnumerable<string>? vary)
            || vary.SelectMany(value => value.Split(',')).All(name => CacheHeaderNames.Contains(name.Trim())));
}
