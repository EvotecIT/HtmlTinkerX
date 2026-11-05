using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Reads pages politely and says why a publisher refused them. Applies an <see cref="HtmlFetchPolicy"/>: per-host
/// pacing through <see cref="HtmlHostRequestGate"/>, retries of GET and HEAD after transient failures (honouring a
/// bounded Retry-After), its own redirects so every hop is gated and checked, conditional GETs, consistent browser
/// headers, and an <see cref="HtmlFetchPolicy.OnBlocked"/> decision when a publisher challenges or blocks a request.
/// </summary>
/// <remarks>
/// The primary handler must not follow redirects itself (<c>AllowAutoRedirect = false</c>). A Cloudflare challenge is
/// recognised from headers and never has its body read. Non-idempotent methods are never repeated.
/// </remarks>
public sealed class HtmlFetchHandler : DelegatingHandler {
    /// <summary>Creates a handler; set <see cref="DelegatingHandler.InnerHandler"/> before use.</summary>
    /// <param name="policy">How requests are paced, retried, redirected and handled when blocked.</param>
    public HtmlFetchHandler(HtmlFetchPolicy policy) {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        policy.Validate();
    }

    /// <summary>Creates a handler over an inner handler, such as an <see cref="HtmlProxyRotator"/>.</summary>
    /// <param name="policy">How requests are paced, retried, redirected and handled when blocked.</param>
    /// <param name="innerHandler">The handler that sends requests; it must not follow redirects automatically.</param>
    public HtmlFetchHandler(HtmlFetchPolicy policy, HttpMessageHandler innerHandler) : base(innerHandler) {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        policy.Validate();
    }

    /// <summary>The policy this handler applies.</summary>
    public HtmlFetchPolicy Policy { get; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (request?.RequestUri == null) throw new ArgumentNullException(nameof(request));
        RequireManualRedirects(InnerHandler);
        HttpRequestMessage current = request;
        for (int redirects = 0; ; redirects++) {
            Policy.BrowserHeaders?.Apply(current);
            HttpResponseMessage response = await SendHopAsync(current, cancellationToken).ConfigureAwait(false);
            Uri? target;
            try {
                if (redirects >= HtmlFetchPolicy.MaxRedirects || !CanRedirect(current, response, out target)
                    || Policy.RedirectPolicy is { } policy && !policy(current.RequestUri!, target!)) return response;
            } catch {
                // The response owns its host slot until disposed; a failing policy cannot hand it to the caller.
                response.Dispose();
                if (!ReferenceEquals(current, request)) current.Dispose();
                throw;
            }

            HttpRequestMessage next = CreateRedirect(current, target!);
            response.Dispose();
            if (!ReferenceEquals(current, request)) current.Dispose();
            current = next;
        }
    }

    private async Task<HttpResponseMessage> SendHopAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Uri uri = request.RequestUri!;
        // Only reads are repeated; a POST might have taken effect before it failed.
        bool idempotent = HtmlFetchRules.IsIdempotent(request.Method);
        bool cacheable = Policy.ValidatorCache != null && HtmlFetchRules.CanCacheByAddress(request);
        HtmlHttpValidatorEntry? cached = cacheable ? await ApplyCachedValidatorsAsync(request, cancellationToken).ConfigureAwait(false) : null;
        for (int attempt = 0; ; ) {
            HttpResponseMessage response;
            try {
                response = await SendGatedAsync(uri, request, cancellationToken).ConfigureAwait(false);
            } catch (HttpRequestException ex) when (idempotent && attempt < Policy.RetryCount && ex is not HtmlHttpException) {
                await PauseAsync(uri, NextDelay(null, attempt++), ex.Message, cancellationToken).ConfigureAwait(false);
                continue;
            }

            int status = (int)response.StatusCode;
            HtmlHttpFailure? blocked = await ClassifyBlockedAsync(response, cancellationToken).ConfigureAwait(false);
            if (blocked == null && idempotent && attempt < Policy.RetryCount && HtmlFetchRules.IsTransientStatus(status)) {
                if (HtmlFetchRules.GetServerRetryDelay(response) > Policy.MaximumRetryDelay) return response;
                TimeSpan delay = NextDelay(response, attempt++);
                response.Dispose();
                await PauseAsync(uri, delay, $"HTTP {status}", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (blocked is { } failure) {
                string? contentType = response.Content?.Headers.ContentType?.MediaType;
                // A publisher asking for a longer pause than the policy allows is not waited for, as with transient statuses.
                bool waitable = !(HtmlFetchRules.GetServerRetryDelay(response) > Policy.MaximumRetryDelay);
                TimeSpan delay = NextDelay(response, attempt);
                // A challenge body is never read; disposing releases its host slot before any decision runs.
                response.Dispose();
                HtmlBlockedAction action = Policy.OnBlocked == null
                    ? HtmlBlockedAction.Fail
                    : await Policy.OnBlocked(uri, failure, cancellationToken).ConfigureAwait(false);
                if (action == HtmlBlockedAction.Retry && waitable && idempotent && attempt < Policy.RetryCount) {
                    attempt++;
                    await PauseAsync(uri, delay, failure.Provider ?? failure.Kind.ToString(), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (action == HtmlBlockedAction.Render) return await RenderAsync(request, cancellationToken).ConfigureAwait(false);
                throw new HtmlHttpException(HtmlResponseClassifier.Describe(failure, null, contentType), failure);
            }

            if (cached != null && status == (int)HttpStatusCode.NotModified) {
                if (HtmlFetchRules.CanReuseNotModified(cached.EntityTag, response)) return FromCache(request, response, cached);
                // A mismatched validator cannot authenticate the stored body. Ask once more without the condition.
                response.Dispose();
                request.Headers.IfNoneMatch.Clear();
                request.Headers.IfModifiedSince = null;
                cached = null;
                continue;
            }
            return cacheable ? Store(request, response) : response;
        }
    }

    /// <summary>
    /// The failure to put to <see cref="HtmlFetchPolicy.OnBlocked"/>, or null to handle the response normally: a
    /// challenge, recognised from headers alone, and with a decision to make, a 403 or a recognised block page at any
    /// failing status. Reading a bounded part of a failing body happens only when there is a decision to make.
    /// </summary>
    private async Task<HtmlHttpFailure?> ClassifyBlockedAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        if (HtmlResponseClassifier.IsChallenge(response)) return HtmlResponseClassifier.ClassifyHeaders(response);
        if (Policy.OnBlocked == null || (int)response.StatusCode < 400) return null;
        HtmlHttpFailure failure;
        try {
            failure = (await HtmlResponseClassifier.ClassifyAsync(response, HtmlResponseClassifier.DefaultMaximumBodyBytes, cancellationToken).ConfigureAwait(false))!;
        } catch {
            response.Dispose();
            throw;
        }
        return response.StatusCode == HttpStatusCode.Forbidden || failure.Kind == HtmlHttpFailureKind.BlockPage ? failure : null;
    }

    private TimeSpan NextDelay(HttpResponseMessage? response, int attempt) {
        TimeSpan delay = HtmlFetchRules.GetRetryDelay(response, attempt, Policy.RetryDelay, Policy.MaximumRetryDelay);
        return delay > Policy.MaximumRetryDelay ? Policy.MaximumRetryDelay : delay;
    }

    private async Task PauseAsync(Uri uri, TimeSpan delay, string reason, CancellationToken cancellationToken) {
        Policy.OnRetry?.Invoke(uri.Host, reason);
        await HtmlFetchRules.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends with one of the host's slots taken, and keeps it taken until the response body has been read or the
    /// response disposed.
    /// </summary>
    private async Task<HttpResponseMessage> SendGatedAsync(Uri uri, HttpRequestMessage request, CancellationToken cancellationToken) {
        HtmlHostRequestGate? gate = Policy.Gate;
        if (gate == null) return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        IDisposable lease = await gate.EnterAsync(uri, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage? response = null;
        try {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            gate.ReportResponse(uri, response);
            if (response.Content == null) lease.Dispose();
            else response.Content = new HtmlLeasedContent(response.Content, lease);
            return response;
        } catch {
            try { response?.Dispose(); } finally { lease.Dispose(); }
            throw;
        }
    }

    private async Task<HttpResponseMessage> RenderAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Func<Uri, CancellationToken, Task<HtmlRenderedPage>> renderer = Policy.Renderer
            ?? throw new InvalidOperationException("OnBlocked returned Render, but HtmlFetchPolicy.Renderer is not set.");
        Uri uri = request.RequestUri!;
        IDisposable? lease = Policy.Gate == null ? null : await Policy.Gate.EnterAsync(uri, cancellationToken).ConfigureAwait(false);
        HtmlRenderedPage page;
        try {
            page = await renderer(uri, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("HtmlFetchPolicy.Renderer returned no page.");
        } finally {
            lease?.Dispose();
        }
        // Callers resolve relative links against the response's request address, so it is where the browser ended up.
        Uri final = page.FinalUri ?? uri;
        return new HttpResponseMessage(HttpStatusCode.OK) {
            RequestMessage = final == uri ? request : new HttpRequestMessage(request.Method, final),
            Content = new StringContent(page.Html, Encoding.UTF8, "text/html")
        };
    }

    private async Task<HtmlHttpValidatorEntry?> ApplyCachedValidatorsAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (request.Headers.IfNoneMatch.Count > 0 || request.Headers.IfModifiedSince != null) return null;
        HtmlHttpValidatorEntry? entry = await Policy.ValidatorCache!.GetAsync(request.RequestUri!, cancellationToken).ConfigureAwait(false);
        return entry != null && HtmlFetchRules.ApplyValidators(request, entry.EntityTag, entry.LastModified) ? entry : null;
    }

    private static HttpResponseMessage FromCache(HttpRequestMessage request, HttpResponseMessage notModified, HtmlHttpValidatorEntry cached) {
        HttpResponseMessage response = new(HttpStatusCode.OK) {
            RequestMessage = request,
            Version = notModified.Version,
            Content = new ByteArrayContent(cached.Content)
        };
        foreach (KeyValuePair<string, IEnumerable<string>> header in notModified.Headers) response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (cached.ContentType != null) response.Content.Headers.TryAddWithoutValidation("Content-Type", cached.ContentType);
        notModified.Dispose();
        return response;
    }

    /// <summary>Stores the body with its validators once the caller has read all of it; the headers are returned at once.</summary>
    private HttpResponseMessage Store(HttpRequestMessage request, HttpResponseMessage response) {
        IHtmlHttpValidatorCache cache = Policy.ValidatorCache!;
        if (response.StatusCode != HttpStatusCode.OK || response.Content == null || !HtmlFetchRules.CanStoreByAddress(response)) return response;
        string? entityTag = response.Headers.ETag?.ToString();
        DateTimeOffset? lastModified = response.Content.Headers.LastModified;
        if (string.IsNullOrEmpty(entityTag) && lastModified == null) return response;
        Uri uri = request.RequestUri!;
        string? contentType = response.Content.Headers.ContentType?.ToString();
        response.Content = new HtmlStoringContent(response.Content, HtmlHttpFetchOptions.DefaultMaximumResponseBytes,
            (body, token) => cache.SetAsync(uri, new HtmlHttpValidatorEntry(entityTag, lastModified, body, contentType), token));
        return response;
    }

    private static bool CanRedirect(HttpRequestMessage request, HttpResponseMessage response, out Uri? target) {
        target = null;
        if (!HtmlFetchRules.IsIdempotent(request.Method) || request.RequestUri is not Uri source
            || !HtmlFetchRules.TryGetRedirectTarget(response, source, out Uri? resolved)) return false;
        if (resolved!.Scheme is not ("http" or "https") || source.Scheme == "https" && resolved.Scheme == "http") return false;
        target = resolved;
        return true;
    }

    private static HttpRequestMessage CreateRedirect(HttpRequestMessage current, Uri target) {
        HttpRequestMessage next = new(current.Method, target) { Version = current.Version };
#if NET5_0_OR_GREATER
        next.VersionPolicy = current.VersionPolicy;
#endif
        bool sameOrigin = HtmlUriUtility.HasSameOrigin(current.RequestUri!, target);
        foreach (KeyValuePair<string, IEnumerable<string>> header in current.Headers) {
            // Redirects to another origin must not carry custom credential headers or a pinned Host, and validators
            // belong to the address they were stored for.
            if (!sameOrigin && !IsPortableHeader(header.Key)) continue;
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("If-None-Match", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("If-Modified-Since", StringComparison.OrdinalIgnoreCase)) continue;
            next.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return next;
    }

    private static bool IsPortableHeader(string name) =>
        name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) || name.Equals("Accept", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rejects a primary handler that would follow redirects before this handler can gate and check them.</summary>
    internal static void RequireManualRedirects(HttpMessageHandler? handler) {
        while (handler is DelegatingHandler next) handler = next.InnerHandler;
        if (handler is HtmlProxyRotator rotator) {
            foreach (HttpMessageHandler inner in rotator.Handlers) RequireManualRedirects(inner);
            return;
        }
        if (handler is HttpClientHandler { AllowAutoRedirect: true }
#if NET5_0_OR_GREATER
            || handler is SocketsHttpHandler { AllowAutoRedirect: true }
#endif
            ) {
            throw new InvalidOperationException("HtmlFetchHandler requires AllowAutoRedirect=false on the primary handler so every redirect is gated and checked.");
        }
    }
}
