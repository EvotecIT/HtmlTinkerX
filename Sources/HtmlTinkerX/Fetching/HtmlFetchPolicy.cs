using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// How <see cref="HtmlFetchHandler"/> reads pages: host pacing, retries, redirects, revalidation, browser headers and
/// what to do when a publisher blocks the request. Pass it to <see cref="HtmlHttpClientFactory.Create(HtmlFetchPolicy, HttpMessageHandler?)"/>.
/// </summary>
public sealed class HtmlFetchPolicy {
    /// <summary>Pause before the first retry when the site does not say how long to wait.</summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(4);

    /// <summary>Longest Retry-After honoured by default.</summary>
    public static readonly TimeSpan DefaultMaximumRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Redirects followed for one request before the last redirect is returned as it is.</summary>
    public const int MaxRedirects = 10;

    /// <summary>Host and aggregate limits shared by every handler reading for the same process, or null for no limits.</summary>
    public HtmlHostRequestGate? Gate { get; set; }

    /// <summary>
    /// Retries of a GET or HEAD after a transient status (408, 429, 500, 502, 503, 504), a dropped connection, or an
    /// <see cref="OnBlocked"/> decision to retry. Other methods are never repeated. One by default; at most ten.
    /// </summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>Pause before the first retry when the site gives none; later retries double it up to <see cref="MaximumRetryDelay"/>.</summary>
    public TimeSpan RetryDelay { get; set; } = DefaultRetryDelay;

    /// <summary>Longest Retry-After honoured; a site asking for more gets its response returned instead of stalling the caller.</summary>
    public TimeSpan MaximumRetryDelay { get; set; } = DefaultMaximumRetryDelay;

    /// <summary>
    /// Decides whether a redirect from the first address to the second is followed; every HTTP(S) redirect that does
    /// not downgrade HTTPS is followed when null. A refused redirect is returned as it is, with its Location, so the
    /// caller can report it. <see cref="AllowHosts"/> builds a host list.
    /// </summary>
    public Func<Uri, Uri, bool>? RedirectPolicy { get; set; }

    /// <summary>Stores entity tags and Last-Modified dates so repeated GETs revalidate and reuse an unchanged body; null disables it.</summary>
    public IHtmlHttpValidatorCache? ValidatorCache { get; set; }

    /// <summary>Consistent browser request headers applied to every request; null keeps the client's own headers.</summary>
    public HtmlBrowserHeaderProfile? BrowserHeaders { get; set; }

    /// <summary>Called with the host and the reason before each retry, for logging.</summary>
    public Action<string, string>? OnRetry { get; set; }

    /// <summary>
    /// Decides what happens when a publisher challenges or blocks a request (a Cloudflare challenge, a recognised CDN
    /// block page, or another 403). Without it a challenge throws <see cref="HtmlHttpException"/> before its body is
    /// read and other refusals are returned as they are.
    /// </summary>
    public Func<Uri, HtmlHttpFailure, CancellationToken, Task<HtmlBlockedAction>>? OnBlocked { get; set; }

    /// <summary>
    /// Produces a page's HTML some other way, normally a real browser, when <see cref="OnBlocked"/> returns
    /// <see cref="HtmlBlockedAction.Render"/>. <see cref="HtmlBrowser.CreateFetchRenderer"/> provides one.
    /// </summary>
    public Func<Uri, CancellationToken, Task<string>>? Renderer { get; set; }

    /// <summary>
    /// A <see cref="RedirectPolicy"/> that follows redirects only to the given hosts, compared without case, and to
    /// their subdomains when <paramref name="includeSubdomains"/> is set. Unicode and punycode names are the same host.
    /// </summary>
    /// <param name="hosts">Allowed host names.</param>
    /// <param name="includeSubdomains">Whether subdomains of the allowed hosts are allowed too.</param>
    public static Func<Uri, Uri, bool> AllowHosts(IEnumerable<string> hosts, bool includeSubdomains = false) {
        if (hosts == null) throw new ArgumentNullException(nameof(hosts));
        HashSet<string> allowed = new(hosts.Where(static host => !string.IsNullOrWhiteSpace(host))
            .Select(static host => new UriBuilder(Uri.UriSchemeHttps, host.Trim().TrimEnd('.')).Uri.IdnHost.TrimEnd('.')),
            StringComparer.OrdinalIgnoreCase);
        return (_, target) => {
            string host = target.IdnHost.TrimEnd('.');
            if (allowed.Contains(host)) return true;
            if (!includeSubdomains) return false;
            for (int dot = host.IndexOf('.'); dot >= 0; dot = host.IndexOf('.', dot + 1)) {
                if (allowed.Contains(host.Substring(dot + 1))) return true;
            }
            return false;
        };
    }

    internal void Validate() {
        if (RetryCount < 0 || RetryCount > 10) throw new ArgumentOutOfRangeException(nameof(RetryCount), RetryCount, "RetryCount must be between 0 and 10.");
        if (RetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RetryDelay));
        if (MaximumRetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumRetryDelay));
    }
}
