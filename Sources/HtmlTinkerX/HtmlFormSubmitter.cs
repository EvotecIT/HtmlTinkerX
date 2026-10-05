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
    /// Fills form fields and submits using Playwright.
    /// </summary>
    /// <param name="page">Playwright page containing the form.</param>
    /// <param name="formSelector">CSS selector identifying the form.</param>
    /// <param name="fields">Field values keyed by name attribute.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SubmitAsync(IPage page, string formSelector, IDictionary<string, string> fields, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }
        if (formSelector == null) {
            throw new ArgumentNullException(nameof(formSelector));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var form = page.Locator(formSelector);
        await WithCancellation(form.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout }), cancellationToken).ConfigureAwait(false);
        foreach (var kv in fields) {
            var input = form.Locator($":scope [name=\"{kv.Key}\"]");
            await WithCancellation(input.FillAsync(kv.Value, new LocatorFillOptions { Timeout = timeout }), cancellationToken).ConfigureAwait(false);
        }
        await WithCancellation(form.EvaluateAsync("form => form.submit()"), cancellationToken).ConfigureAwait(false);
        await WithCancellation(page.WaitForLoadStateAsync(LoadState.NetworkIdle), cancellationToken).ConfigureAwait(false);
    }

#if NET6_0_OR_GREATER
    private static Task WithCancellation(Task task, CancellationToken cancellationToken) => task.WaitAsync(cancellationToken);
    private static Task<T> WithCancellation<T>(Task<T> task, CancellationToken cancellationToken) => task.WaitAsync(cancellationToken);
#else
    private static async Task WithCancellation(Task task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled) {
            await task.ConfigureAwait(false);
            return;
        }
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs)) {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)) {
                throw new TaskCanceledException();
            }
        }
        await task.ConfigureAwait(false);
    }

    private static async Task<T> WithCancellation<T>(Task<T> task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled) {
            return await task.ConfigureAwait(false);
        }
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs)) {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)) {
                throw new TaskCanceledException();
            }
        }
        return await task.ConfigureAwait(false);
    }
#endif

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
