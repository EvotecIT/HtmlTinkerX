using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Follows deterministic browserless hidden-form relay responses with an HTTP client.
/// </summary>
public static class HtmlFormRelayClient {
    /// <summary>
    /// Follows hidden auto-submit relay forms until no relay form remains or a safety limit is reached.
    /// </summary>
    /// <param name="html">Initial response content.</param>
    /// <param name="responseUri">Initial response URI.</param>
    /// <param name="client">HTTP client used to submit relay forms. Use a cookie-enabled client to preserve sessions.</param>
    /// <param name="options">Relay options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final content and relay diagnostics.</returns>
    public static async Task<HtmlFormRelayResult> FollowAsync(
        string html,
        Uri responseUri,
        HttpClient? client = null,
        HtmlFormRelayOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (responseUri == null) {
            throw new ArgumentNullException(nameof(responseUri));
        }

        HtmlFormRelayOptions effectiveOptions = options ?? new HtmlFormRelayOptions();
        if (effectiveOptions.MaxRelayCount < 1) {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRelayCount must be at least 1.");
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string currentHtml = html;
        Uri currentUri = responseUri;
        List<HtmlFormRelayStep> steps = new();

        for (int index = 0; index < effectiveOptions.MaxRelayCount; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HtmlFormRelayParser.TryParse(currentHtml, currentUri, out HtmlFormRelayRequest? request) || request == null) {
                return CreateResult(currentHtml, currentUri, HtmlFormRelayStopReason.NoRelayForm, steps);
            }

            bool isCrossHost = !string.Equals(currentUri.Host, request.ActionUri.Host, StringComparison.OrdinalIgnoreCase);
            bool isCrossOrigin = !HasSameOrigin(currentUri, request.ActionUri);
            HtmlFormRelayStep step = CreateStep(index, request, isCrossHost, isCrossOrigin);
            if (isCrossOrigin && !IsCrossOriginAllowed(request.ActionUri, effectiveOptions)) {
                step.Blocked = true;
                steps.Add(step);
                return CreateResult(currentHtml, currentUri, HtmlFormRelayStopReason.CrossHostBlocked, steps);
            }

            using HttpResponseMessage response = await SendAsync(http, request, cancellationToken).ConfigureAwait(false);
            step.StatusCode = (int)response.StatusCode;
            step.ResponseUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? request.ActionUri.AbsoluteUri;
            steps.Add(step);

            currentUri = response.RequestMessage?.RequestUri ?? request.ActionUri;
            currentHtml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        return CreateResult(currentHtml, currentUri, HtmlFormRelayStopReason.MaxRelayCountReached, steps);
    }

    private static HtmlFormRelayResult CreateResult(string content, Uri uri, HtmlFormRelayStopReason reason, IReadOnlyList<HtmlFormRelayStep> steps) =>
        new() {
            FinalContent = content,
            FinalUrl = uri.AbsoluteUri,
            StopReason = reason,
            SubmittedRelay = steps.Any(static step => !step.Blocked),
            Steps = steps.ToArray()
        };

    private static HtmlFormRelayStep CreateStep(int index, HtmlFormRelayRequest request, bool isCrossHost, bool isCrossOrigin) =>
        new() {
            Index = index,
            Method = request.Method,
            ActionUrl = request.ActionUri.AbsoluteUri,
            FieldNames = request.FieldNames,
            ProtocolHint = request.ProtocolHint,
            IsCrossHost = isCrossHost,
            IsCrossOrigin = isCrossOrigin
        };

    private static bool HasSameOrigin(Uri currentUri, Uri actionUri) =>
        string.Equals(currentUri.Scheme, actionUri.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(currentUri.Host, actionUri.Host, StringComparison.OrdinalIgnoreCase)
        && currentUri.Port == actionUri.Port;

    private static bool IsCrossOriginAllowed(Uri actionUri, HtmlFormRelayOptions options) =>
        options.AllowCrossHost
        || options.AllowedHosts.Any(host => string.Equals(host, actionUri.Host, StringComparison.OrdinalIgnoreCase));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HtmlFormRelayRequest request, CancellationToken cancellationToken) {
        if (request.Method == FormMethod.Get) {
            Uri uri = await BuildGetUriAsync(request.ActionUri, request.Fields).ConfigureAwait(false);
            return await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        using FormUrlEncodedContent content = new(request.Fields);
        return await client.PostAsync(request.ActionUri, content, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Uri> BuildGetUriAsync(Uri actionUri, IReadOnlyDictionary<string, string> fields) {
        UriBuilder builder = new(actionUri);
        List<KeyValuePair<string, string>> parameters = new();
        if (!string.IsNullOrEmpty(builder.Query)) {
            foreach (string pair in builder.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
                string[] kv = pair.Split(new[] { '=' }, 2);
                parameters.Add(new KeyValuePair<string, string>(
                    Uri.UnescapeDataString(kv[0]),
                    kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty));
            }
        }

        foreach (KeyValuePair<string, string> field in fields) {
            parameters.Add(field);
        }

        using FormUrlEncodedContent queryContent = new(parameters);
        builder.Query = await queryContent.ReadAsStringAsync().ConfigureAwait(false);
        return builder.Uri;
    }
}
