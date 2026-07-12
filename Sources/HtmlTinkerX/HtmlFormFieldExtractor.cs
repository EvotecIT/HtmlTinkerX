using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting all input fields from HTML forms.
/// </summary>
public static class HtmlFormFieldExtractor {
    /// <summary>
    /// Extracts form fields using AngleSharp from provided HTML.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>List of form fields.</returns>
    public static List<HtmlFormField> ExtractFields(string? html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var fields = document.QuerySelectorAll("form input,form select,form textarea,form button");
        List<HtmlFormField> results = new();
        foreach (var field in fields) {
            string? name = field.GetAttribute("name");
            if (name == null || name.Length == 0) {
                continue;
            }
            string nameValue = name;
            string type = field.GetAttribute("type") ?? field.NodeName.ToLowerInvariant();
            results.Add(new HtmlFormField {
                Name = nameValue,
                Type = HtmlFormFieldUtilities.MapType(type),
                Value = HtmlFormFieldUtilities.GetSubmittedValue(field)
            });
        }
        return results;
    }

    /// <summary>
    /// Downloads HTML from a URL and extracts form fields.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of form fields.</returns>
    public static async Task<List<HtmlFormField>> ExtractUrlFieldsAsync(string? url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        return ExtractFields(content);
    }
}
