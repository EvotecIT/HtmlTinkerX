using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides functionality for extracting form information from HTML.
/// </summary>
public static partial class HtmlParserFromForm {
    /// <summary>
    /// Parses HTML and extracts forms with their fields using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing forms.</param>
    /// <returns>List of form parse results.</returns>
    /// <example>
    /// <code>
    /// var forms = HtmlParserFromForm.ParseFormsWithAngleSharp(html);
    /// </code>
    /// </example>
    public static List<HtmlFormResult> ParseFormsWithAngleSharp(string? html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var forms = document.QuerySelectorAll("form");
        List<HtmlFormResult> results = new();
        int index = 0;
        foreach (var form in forms) {
            HtmlFormResult result = new();
            var metadata = result.Metadata;
            metadata.FormIndex = index++;
            metadata.Id = form.Id;
            metadata.Classes = form.ClassName;
            metadata.Action = form.GetAttribute("action") ?? string.Empty;
            string m = form.GetAttribute("method")?.ToUpperInvariant() ?? "GET";
            metadata.Method = m == "POST" ? FormMethod.Post : FormMethod.Get;

            foreach (var field in form.QuerySelectorAll("input,select,textarea,button")) {
                string? name = field.GetAttribute("name");
                if (name == null || name.Length == 0) {
                    continue;
                }
                string nameValue = name;
                string type = field.GetAttribute("type") ?? field.NodeName.ToLowerInvariant();
                result.Fields.Add(new HtmlFormField {
                    Name = nameValue,
                    Type = HtmlFormFieldUtilities.MapType(type),
                    Value = HtmlFormFieldUtilities.GetSubmittedValue(field)
                });
            }
            results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// Downloads HTML from a URL and parses forms using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of form parse results.</returns>
    /// <example>
    /// <code>
    /// var forms = await HtmlParserFromForm.ParseUrlFormsWithAngleSharpAsync(url);
    /// </code>
    /// </example>
    public static async Task<List<HtmlFormResult>> ParseUrlFormsWithAngleSharpAsync(string? url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        return ParseFormsWithAngleSharp(content);
    }
}
