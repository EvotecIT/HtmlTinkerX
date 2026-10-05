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

        return ParseFormsWithAngleSharp(html, sourceUri: null);
    }

    /// <summary>Parses forms using an absolute document address to resolve relative actions.</summary>
    /// <param name="html">HTML content containing forms.</param>
    /// <param name="sourceUri">Document address, or null when the address is unknown.</param>
    /// <returns>Forms, their field inventory, and ordered successful values.</returns>
    public static List<HtmlFormResult> ParseFormsWithAngleSharp(string? html, Uri? sourceUri) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        if (sourceUri != null && !sourceUri.IsAbsoluteUri) {
            throw new ArgumentException("The document address must be absolute.", nameof(sourceUri));
        }
        return ParseFormsDocument(HtmlParser.ParseWithAngleSharp(html), sourceUri, sourceUri);
    }

    internal static List<HtmlFormResult> ParseFormsDocument(IDocument document, Uri? sourceUri = null, Uri? finalUri = null) {
        var forms = document.QuerySelectorAll("form");
        var controlsByForm = HtmlFormControlUtilities.GetControlsByForm(document);
        List<HtmlFormResult> results = new();
        int index = 0;
        foreach (var form in forms) {
            HtmlFormResult result = new();
            var metadata = result.Metadata;
            metadata.FormIndex = index++;
            metadata.Id = form.Id;
            metadata.Classes = form.ClassName;
            metadata.Action = form.GetAttribute("action") ?? string.Empty;
            metadata.SourceUri = sourceUri;
            metadata.FinalUri = finalUri;
            metadata.BaseUri = finalUri == null ? null : HtmlFormUrlUtilities.GetEffectiveBaseUri(document, finalUri);
            Uri? actionBase = string.IsNullOrWhiteSpace(metadata.Action) ? finalUri : metadata.BaseUri;
            if (actionBase != null && HtmlFormUrlUtilities.TryResolveAction(metadata.Action, actionBase, out Uri actionUri)) {
                metadata.ResolvedActionUri = actionUri;
            } else if (Uri.TryCreate(metadata.Action, UriKind.Absolute, out Uri? absoluteAction)
                && (absoluteAction.Scheme == Uri.UriSchemeHttp || absoluteAction.Scheme == Uri.UriSchemeHttps)) {
                metadata.ResolvedActionUri = absoluteAction;
            }
            string m = form.GetAttribute("method")?.ToUpperInvariant() ?? "GET";
            metadata.Method = m == "POST" ? FormMethod.Post : FormMethod.Get;

            List<IElement> controls = controlsByForm.TryGetValue(form, out List<IElement>? associatedControls)
                ? associatedControls : new List<IElement>();
            foreach (var field in controls) {
                string? name = field.GetAttribute("name");
                if (name == null || name.Length == 0) {
                    continue;
                }
                string nameValue = name;
                result.Fields.Add(new HtmlFormField {
                    Name = nameValue,
                    Type = HtmlFormFieldUtilities.GetFieldType(field),
                    Value = HtmlFormFieldUtilities.GetSubmittedValue(field)
                });
            }
            result.SuccessfulFields = HtmlFormControlUtilities.GetSubmittedValues(HtmlFormControlUtilities.GetSuccessfulControls(controls));
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
        HtmlHttpTextResult response = await HtmlUtilities.GetTextWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
        Uri? sourceUri = Uri.TryCreate(url, UriKind.Absolute, out Uri? requestedUri)
            ? requestedUri
            : http.BaseAddress == null ? null : new Uri(http.BaseAddress, url);
        return ParseFormsDocument(HtmlParser.ParseWithAngleSharp(response.Content), sourceUri, response.FinalUri ?? sourceUri);
    }
}
