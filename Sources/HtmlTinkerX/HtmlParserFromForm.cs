using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides functionality for extracting form information from HTML.
/// </summary>
public static class HtmlParserFromForm {
    private static readonly HttpClient _sharedClient = new();

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
    public static List<HtmlFormResult> ParseFormsWithAngleSharp(string html) {
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
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                string type = field.GetAttribute("type") ?? field.NodeName.ToLowerInvariant();
                result.Fields.Add(new HtmlFormField {
                    Name = name!,
                    Type = MapType(type)
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
    /// <returns>List of form parse results.</returns>
    /// <example>
    /// <code>
    /// var forms = await HtmlParserFromForm.ParseUrlFormsWithAngleSharpAsync(url);
    /// </code>
    /// </example>
    public static async Task<List<HtmlFormResult>> ParseUrlFormsWithAngleSharpAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? _sharedClient;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseFormsWithAngleSharp(content);
    }

    private static HtmlFormFieldType MapType(string? type) {
        return type?.ToLowerInvariant() switch {
            "text" => HtmlFormFieldType.Text,
            "password" => HtmlFormFieldType.Password,
            "hidden" => HtmlFormFieldType.Hidden,
            "checkbox" => HtmlFormFieldType.Checkbox,
            "radio" => HtmlFormFieldType.Radio,
            "submit" => HtmlFormFieldType.Submit,
            "select" => HtmlFormFieldType.Select,
            "textarea" => HtmlFormFieldType.Textarea,
            "button" => HtmlFormFieldType.Button,
            _ => HtmlFormFieldType.Other,
        };
    }
}