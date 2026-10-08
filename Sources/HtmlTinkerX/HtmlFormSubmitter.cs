using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for submitting HTML forms either using Playwright or direct HTTP requests.
/// </summary>
public static partial class HtmlFormSubmitter {
    /// <summary>
    /// Submits a form via HTTP request using the provided action and method.
    /// </summary>
    /// <param name="actionUrl">Form action URL.</param>
    /// <param name="method">Submission method.</param>
    /// <param name="fields">Field values keyed by name.</param>
    /// <param name="client">Optional HttpClient instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response body as string.</returns>
    public static Task<string> SubmitAsync(string actionUrl, FormMethod method, IDictionary<string, string> fields, HttpClient? client = null, CancellationToken cancellationToken = default) =>
        SubmitAsync(actionUrl, method, fields, client, fetchOptions: null, cancellationToken);

    /// <summary>Submits an HTTP form and reads its response with explicit byte limits and HTML decoding.</summary>
    /// <param name="actionUrl">Form action URL.</param>
    /// <param name="method">Submission method.</param>
    /// <param name="fields">Field values keyed by name.</param>
    /// <param name="client">Optional reusable HTTP client.</param>
    /// <param name="fetchOptions">Response byte policy; null uses the default 16 MiB limit.</param>
    /// <param name="cancellationToken">Cancellation covering request headers and the response body.</param>
    /// <returns>Decoded HTML response.</returns>
    public static Task<string> SubmitAsync(string actionUrl, FormMethod method, IDictionary<string, string> fields, HttpClient? client, HtmlHttpFetchOptions? fetchOptions, CancellationToken cancellationToken) =>
        SubmitAsync(actionUrl, method, (IEnumerable<KeyValuePair<string, string>>)fields, client, fetchOptions, cancellationToken);

    /// <summary>Submits ordered HTTP form values, retaining repeated names.</summary>
    /// <param name="actionUrl">Form action URL. GET submissions append fields to its existing query.</param>
    /// <param name="method">Submission method.</param>
    /// <param name="fields">Ordered named values; names may repeat.</param>
    /// <param name="client">Optional reusable HTTP client, owned by the caller.</param>
    /// <param name="cancellationToken">Cancellation covering request headers and the response body.</param>
    /// <returns>Decoded HTML response.</returns>
    public static Task<string> SubmitAsync(string actionUrl, FormMethod method, IEnumerable<KeyValuePair<string, string>> fields, HttpClient? client = null, CancellationToken cancellationToken = default) =>
        SubmitAsync(actionUrl, method, fields, client, fetchOptions: null, cancellationToken);

    /// <summary>Submits ordered HTTP form values with explicit response byte limits and HTML decoding.</summary>
    /// <param name="actionUrl">Form action URL. GET submissions append fields to its existing query.</param>
    /// <param name="method">Submission method.</param>
    /// <param name="fields">Ordered named values; names may repeat.</param>
    /// <param name="client">Optional reusable HTTP client, owned by the caller.</param>
    /// <param name="fetchOptions">Response byte policy; null uses the default 16 MiB limit.</param>
    /// <param name="cancellationToken">Cancellation covering request headers and the response body.</param>
    /// <returns>Decoded HTML response.</returns>
    public static async Task<string> SubmitAsync(string actionUrl, FormMethod method, IEnumerable<KeyValuePair<string, string>> fields, HttpClient? client, HtmlHttpFetchOptions? fetchOptions, CancellationToken cancellationToken) {
        if (actionUrl == null) {
            throw new ArgumentNullException(nameof(actionUrl));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        cancellationToken.ThrowIfCancellationRequested();
        fetchOptions?.GetValidatedMaximumResponseBytes();

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        using CancellationTokenSource deadline = HtmlUtilities.CreateRequestTimeoutTokenSource(http, cancellationToken);
        CancellationToken requestToken = deadline.Token;
        using HttpRequestMessage request = new(method == FormMethod.Get ? HttpMethod.Get : HttpMethod.Post, actionUrl);
        if (method == FormMethod.Get) {
            var builder = new UriBuilder(actionUrl);
            var parameters = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(builder.Query)) {
                string[] existing = builder.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string pair in existing) {
                    string[] kv = pair.Split(new[] { '=' }, 2);
                    string key = WebUtility.UrlDecode(kv[0]);
                    string value = kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : string.Empty;
                    parameters.Add(new KeyValuePair<string, string>(key, value));
                }
            }
            foreach (var kv in fields) {
                parameters.Add(kv);
            }
            using var queryContent = new FormUrlEncodedContent(parameters);
            builder.Query = await queryContent.ReadAsStringAsync().ConfigureAwait(false);
            request.RequestUri = builder.Uri;
        } else {
            request.Content = new FormUrlEncodedContent(fields);
        }
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await HtmlUtilities.ReadResponseContentWithProperEncodingAsync(response, fetchOptions, requestToken).ConfigureAwait(false);
    }
}
