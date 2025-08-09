using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for submitting HTML forms either using Playwright or direct HTTP requests.
/// </summary>
public static class HtmlFormSubmitter {
    /// <summary>
    /// Fills form fields and submits using Playwright.
    /// </summary>
    /// <param name="page">Playwright page containing the form.</param>
    /// <param name="formSelector">CSS selector identifying the form.</param>
    /// <param name="fields">Field values keyed by name attribute.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    public static async Task SubmitAsync(IPage page, string formSelector, IDictionary<string, string> fields, int timeout = 10000) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }
        if (formSelector == null) {
            throw new ArgumentNullException(nameof(formSelector));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        var form = page.Locator(formSelector);
        await form.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout }).ConfigureAwait(false);
        foreach (var kv in fields) {
            var input = form.Locator($":scope [name=\"{kv.Key}\"]");
            await input.FillAsync(kv.Value, new LocatorFillOptions { Timeout = timeout }).ConfigureAwait(false);
        }
        await form.EvaluateAsync("form => form.submit()").ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
    }

    /// <summary>
    /// Submits a form via HTTP request using the provided action and method.
    /// </summary>
    /// <param name="actionUrl">Form action URL.</param>
    /// <param name="method">Submission method.</param>
    /// <param name="fields">Field values keyed by name.</param>
    /// <param name="client">Optional HttpClient instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response body as string.</returns>
    public static async Task<string> SubmitAsync(string actionUrl, FormMethod method, IDictionary<string, string> fields, HttpClient? client = null, CancellationToken cancellationToken = default) {
        if (actionUrl == null) {
            throw new ArgumentNullException(nameof(actionUrl));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        if (method == FormMethod.Get) {
            var builder = new UriBuilder(actionUrl);
            var parameters = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(builder.Query)) {
                string[] existing = builder.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string pair in existing) {
                    string[] kv = pair.Split(new[] { '=' }, 2);
                    string key = Uri.UnescapeDataString(kv[0]);
                    string value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                    parameters.Add(new KeyValuePair<string, string>(key, value));
                }
            }
            foreach (var kv in fields) {
                parameters.Add(kv);
            }
            using var queryContent = new FormUrlEncodedContent(parameters);
            builder.Query = await queryContent.ReadAsStringAsync().ConfigureAwait(false);
            using HttpResponseMessage response = await http.GetAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        } else {
            using var content = new FormUrlEncodedContent(fields);
            using HttpResponseMessage response = await http.PostAsync(actionUrl, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}