using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Explains why a publisher refused a request. <see cref="IsChallenge"/> and <see cref="EnsureSuccess"/> use headers
/// alone; <see cref="ClassifyAsync"/> also reads a bounded part of an unsuccessful body to recognise CDN block pages.
/// Block pages are recognised only by named provider signatures; an ordinary HTML 403 stays
/// <see cref="HtmlHttpFailureKind.AccessDenied"/>.
/// </summary>
public static class HtmlResponseClassifier {
    /// <summary>Body bytes <see cref="ClassifyAsync"/> reads by default; block pages are small.</summary>
    public const int DefaultMaximumBodyBytes = 64 * 1024;

    private const int TitleLimit = 120;
    private const int ExcerptLimit = 400;
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AbsoluteUrl = new(@"(?i)\b(?:https?|ftp)://[^\s""'<>]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IpAddress = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b|\b(?:[0-9a-f]{1,4}:){2,7}[0-9a-f]{1,4}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    // Azure Front Door prints a tracking reference under its block message: "Ref A: ..." or "20261025T101010Z-abc123...".
    private static readonly Regex AzureReference = new(@"(?i)\bRef [A-C]:|\b\d{8}T\d{6}Z-[0-9a-z]{6,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether the response carries Cloudflare's documented <c>cf-mitigated: challenge</c> signal, irrespective of
    /// status or content type. An ordinary 403, HTML body, server name or script alone does not establish a challenge.
    /// </summary>
    /// <param name="response">The response whose headers are inspected; neither it nor its content is disposed.</param>
    public static bool IsChallenge(HttpResponseMessage response) {
        if (response == null) throw new ArgumentNullException(nameof(response));
        return response.Headers.TryGetValues("cf-mitigated", out IEnumerable<string>? values)
            && values.Any(static value => value.Equals("challenge", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Rejects an explicit challenge, including one returned with a successful status, or any unsuccessful status.
    /// Does not read the body. Successful content still needs the caller's normal parsing and bounds.
    /// </summary>
    /// <param name="response">A caller-owned response, normally obtained with ResponseHeadersRead.</param>
    /// <param name="expectedContent">Optional caller-owned format label, such as RSS/Atom or JSON; never publisher content or credentials.</param>
    /// <exception cref="HtmlHttpException">The response was challenged or its HTTP status was unsuccessful.</exception>
    public static void EnsureSuccess(HttpResponseMessage response, string? expectedContent = null) {
        if (response == null) throw new ArgumentNullException(nameof(response));
        HtmlHttpFailure? failure = ClassifyHeaders(response);
        if (failure != null) throw new HtmlHttpException(Describe(failure, expectedContent, ContentType(response)), failure);
    }

    /// <summary>
    /// Classifies an unsuccessful or challenged response. A Cloudflare challenge is recognised from headers without
    /// reading the body. Otherwise up to <paramref name="maximumBytes"/> of an unsuccessful body are read to recognise
    /// Azure Front Door and Akamai block pages; the response content is replaced so the caller can still read all of it.
    /// </summary>
    /// <param name="response">A caller-owned response.</param>
    /// <param name="maximumBytes">Most body bytes inspected for a block page.</param>
    /// <param name="cancellationToken">Cancels the bounded body read.</param>
    /// <returns>The failure, or null when the response is successful and not challenged.</returns>
    public static async Task<HtmlHttpFailure?> ClassifyAsync(HttpResponseMessage response, int maximumBytes = DefaultMaximumBodyBytes,
        CancellationToken cancellationToken = default) {
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes), maximumBytes, "Maximum bytes must be greater than zero.");
        HtmlHttpFailure? failure = ClassifyHeaders(response);
        if (failure == null || failure.Kind == HtmlHttpFailureKind.Challenge || !MayBeHtml(response)) return failure;

        (byte[] head, _) = await HtmlReplayContent.BufferAsync(response, maximumBytes, cancellationToken).ConfigureAwait(false);
        return ClassifyBlockPage(response, head) ?? failure;
    }

    internal static HtmlHttpFailure? ClassifyHeaders(HttpResponseMessage response) {
        bool challenge = IsChallenge(response);
        if (!challenge && response.IsSuccessStatusCode) return null;
        HtmlHttpFailureKind kind = challenge ? HtmlHttpFailureKind.Challenge : KindFromStatus(response.StatusCode);
        // Status and the parsed media type are sufficient diagnostics. Do not retain a request URL, reason phrase,
        // response body or cookies: those can contain session information even on otherwise public sources.
        return new HtmlHttpFailure(kind, response.StatusCode, challenge ? "Cloudflare" : null, RetryAt(response));
    }

    private static HtmlHttpFailureKind KindFromStatus(HttpStatusCode status) => status switch {
        HttpStatusCode.Unauthorized => HtmlHttpFailureKind.AuthenticationRequired,
        HttpStatusCode.Forbidden => HtmlHttpFailureKind.AccessDenied,
        (HttpStatusCode)429 => HtmlHttpFailureKind.RateLimited,
        _ => HtmlHttpFailureKind.HttpError
    };

    private static HtmlHttpFailure? ClassifyBlockPage(HttpResponseMessage response, byte[] head) {
        if (head.Length == 0) return null;
        string html = HtmlUtilities.DecodeHtmlResponse(head, response.Content?.Headers.ContentType?.CharSet);
        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        foreach (IElement element in document.QuerySelectorAll("script,style,noscript,template").ToArray()) element.Remove();
        string title = Collapse(document.Title ?? string.Empty);
        string text = Collapse(document.Body?.TextContent ?? document.DocumentElement?.TextContent ?? string.Empty);

        string? provider = null;
        if (Contains(text, "The request is blocked")
            && (response.Headers.Contains("x-azure-ref") || response.Headers.Contains("x-msedge-ref") || AzureReference.IsMatch(text))) {
            provider = "Azure Front Door";
        } else if ((Contains(title, "Access Denied") || Contains(text, "Access Denied"))
            && Contains(text, "You don't have permission to access") && Contains(text, "Reference #")) {
            provider = "Akamai";
        }
        if (provider == null) return null;

        return new HtmlHttpFailure(HtmlHttpFailureKind.BlockPage, response.StatusCode, provider, RetryAt(response),
            Shorten(Redact(title), TitleLimit), Shorten(Redact(text), ExcerptLimit));
    }

    private static bool MayBeHtml(HttpResponseMessage response) {
        if (response.Content == null) return false;
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType == null || mediaType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0
            || mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string term) => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string Collapse(string value) => Whitespace.Replace(value, " ").Trim();

    private static string Redact(string value) {
        string redacted = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(value);
        redacted = AbsoluteUrl.Replace(redacted, "<url>");
        return IpAddress.Replace(redacted, "<ip>");
    }

    private static string? Shorten(string value, int limit) =>
        value.Length == 0 ? null : value.Length <= limit ? value : value.Substring(0, limit).TrimEnd();

    private static string? ContentType(HttpResponseMessage response) => response.Content?.Headers.ContentType?.MediaType;

    internal static string Describe(HtmlHttpFailure failure, string? expectedContent, string? contentType = null) {
        string facts = contentType == null ? $"HTTP {(int)failure.StatusCode}" : $"HTTP {(int)failure.StatusCode}; content type {contentType}";
        string expected = string.IsNullOrWhiteSpace(expectedContent) ? "the requested content" : expectedContent!;
        switch (failure.Kind) {
            case HtmlHttpFailureKind.Challenge:
                return $"Publisher returned a {failure.Provider ?? "publisher"} verification page instead of {expected} ({facts}). Ask the publisher to allow automated access, or render the page in a browser.";
            case HtmlHttpFailureKind.BlockPage:
                return $"Publisher's {failure.Provider} block page was returned instead of {expected} ({facts}). The request was blocked by the publisher's CDN or firewall.";
        }
        string reason = failure.Kind switch {
            HtmlHttpFailureKind.AuthenticationRequired => "The publisher requires authentication.",
            HtmlHttpFailureKind.AccessDenied => "The publisher refused this request.",
            HtmlHttpFailureKind.RateLimited => "The publisher is rate limiting requests.",
            _ when failure.StatusCode >= HttpStatusCode.InternalServerError => "The publisher's service failed to serve the page.",
            _ => "Check the URL and publisher availability."
        };
        return $"Request failed ({facts}). {reason}";
    }

    internal static DateTimeOffset? RetryAt(HttpResponseMessage response) {
        var retry = response.Headers.RetryAfter;
        if (retry?.Date is DateTimeOffset date) return date;
        if (retry?.Delta is not TimeSpan delta) return null;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return delta > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now.Add(delta);
    }
}
