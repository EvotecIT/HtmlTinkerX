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
        HtmlHttpValidatorEntry? cached = await ApplyCachedValidatorsAsync(request, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; ) {
            HttpResponseMessage response;
            try {
                response = await SendGatedAsync(uri, request, cancellationToken).ConfigureAwait(false);
            } catch (HttpRequestException ex) when (idempotent && attempt < Policy.RetryCount && ex is not HtmlHttpException) {
                await PauseAsync(uri, NextDelay(null, attempt++), ex.Message, cancellationToken).ConfigureAwait(false);
                continue;
            }

            int status = (int)response.StatusCode;
            bool challenge = HtmlResponseClassifier.IsChallenge(response);
            if (!challenge && idempotent && attempt < Policy.RetryCount && HtmlFetchRules.IsTransientStatus(status)) {
                if (HtmlFetchRules.GetServerRetryDelay(response) > Policy.MaximumRetryDelay) return response;
                TimeSpan delay = NextDelay(response, attempt++);
                response.Dispose();
                await PauseAsync(uri, delay, $"HTTP {status}", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (challenge || (status == (int)HttpStatusCode.Forbidden && Policy.OnBlocked != null)) {
                HtmlHttpFailure failure;
                try {
                    failure = challenge
                        ? HtmlResponseClassifier.ClassifyHeaders(response)!
                        : (await HtmlResponseClassifier.ClassifyAsync(response, HtmlResponseClassifier.DefaultMaximumBodyBytes, cancellationToken).ConfigureAwait(false))!;
                } catch {
                    response.Dispose();
                    throw;
                }
                string? contentType = response.Content?.Headers.ContentType?.MediaType;
                TimeSpan delay = NextDelay(response, attempt);
                // A challenge body is never read; disposing releases its host slot before any decision runs.
                response.Dispose();
                HtmlBlockedAction action = Policy.OnBlocked == null
                    ? HtmlBlockedAction.Fail
                    : await Policy.OnBlocked(uri, failure, cancellationToken).ConfigureAwait(false);
                if (action == HtmlBlockedAction.Retry && idempotent && attempt < Policy.RetryCount) {
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
            return await StoreAsync(request, response, cancellationToken).ConfigureAwait(false);
        }
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
        Func<Uri, CancellationToken, Task<string>> renderer = Policy.Renderer
            ?? throw new InvalidOperationException("OnBlocked returned Render, but HtmlFetchPolicy.Renderer is not set.");
        Uri uri = request.RequestUri!;
        IDisposable? lease = Policy.Gate == null ? null : await Policy.Gate.EnterAsync(uri, cancellationToken).ConfigureAwait(false);
        string html;
        try {
            html = await renderer(uri, cancellationToken).ConfigureAwait(false);
        } finally {
            lease?.Dispose();
        }
        return new HttpResponseMessage(HttpStatusCode.OK) {
            RequestMessage = request,
            Content = new StringContent(html ?? string.Empty, Encoding.UTF8, "text/html")
        };
    }

    private async Task<HtmlHttpValidatorEntry?> ApplyCachedValidatorsAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (Policy.ValidatorCache == null || request.Method != HttpMethod.Get
            || request.Headers.IfNoneMatch.Count > 0 || request.Headers.IfModifiedSince != null) return null;
        HtmlHttpValidatorEntry? entry = await Policy.ValidatorCache.GetAsync(request.RequestUri!, cancellationToken).ConfigureAwait(false);
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

    private async Task<HttpResponseMessage> StoreAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken) {
        IHtmlHttpValidatorCache? cache = Policy.ValidatorCache;
        if (cache == null || request.Method != HttpMethod.Get || response.StatusCode != HttpStatusCode.OK || response.Content == null
            || !HtmlFetchRules.CanStoreResponse(response)) return response;
        string? entityTag = response.Headers.ETag?.ToString();
        DateTimeOffset? lastModified = response.Content.Headers.LastModified;
        if (string.IsNullOrEmpty(entityTag) && lastModified == null) return response;
        try {
            (byte[] bytes, bool complete) = await HtmlReplayContent.BufferAsync(response, HtmlHttpFetchOptions.DefaultMaximumResponseBytes, cancellationToken).ConfigureAwait(false);
            if (complete) {
                await cache.SetAsync(request.RequestUri!, new HtmlHttpValidatorEntry(entityTag, lastModified, bytes,
                    response.Content.Headers.ContentType?.ToString()), cancellationToken).ConfigureAwait(false);
            }
            return response;
        } catch {
            response.Dispose();
            throw;
        }
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
