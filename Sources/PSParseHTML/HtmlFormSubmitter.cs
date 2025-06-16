using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

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
        await form.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        foreach (var kv in fields) {
            var input = form.Locator($":scope [name=\"{kv.Key}\"]");
            await input.FillAsync(kv.Value, new LocatorFillOptions { Timeout = timeout });
        }
        await form.EvaluateAsync("form => form.submit()");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Submits a form via HTTP request using the provided action and method.
    /// </summary>
    /// <param name="actionUrl">Form action URL.</param>
    /// <param name="method">Submission method (GET or POST).</param>
    /// <param name="fields">Field values keyed by name.</param>
    /// <param name="client">Optional HttpClient instance.</param>
    /// <returns>Response body as string.</returns>
    public static async Task<string> SubmitAsync(string actionUrl, string? method, IDictionary<string, string> fields, HttpClient? client = null) {
        if (actionUrl == null) {
            throw new ArgumentNullException(nameof(actionUrl));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        method ??= "GET";
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        if (method.Equals("GET", StringComparison.OrdinalIgnoreCase)) {
            string query = string.Join("&", fields.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            string url = actionUrl.Contains("?") ? $"{actionUrl}&{query}" : $"{actionUrl}?{query}";
            using HttpResponseMessage response = await http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        } else {
            using var content = new FormUrlEncodedContent(fields);
            using HttpResponseMessage response = await http.PostAsync(actionUrl, content).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
