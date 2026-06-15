using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Parses deterministic hidden-form relay pages such as WS-Federation or SAML auto-submit responses.
/// </summary>
public static class HtmlFormRelayParser {
    /// <summary>
    /// Attempts to parse a single hidden-form relay request from HTML.
    /// </summary>
    /// <param name="html">HTML content containing a relay form.</param>
    /// <param name="baseUri">Current response URI used to resolve form actions.</param>
    /// <param name="request">Parsed relay request when the page matches the relay shape.</param>
    /// <returns><c>true</c> when a deterministic relay form was found.</returns>
    public static bool TryParse(string html, Uri baseUri, out HtmlFormRelayRequest? request) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (baseUri == null) {
            throw new ArgumentNullException(nameof(baseUri));
        }

        request = null;
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IHtmlCollection<IElement> formElements = document.QuerySelectorAll("form");
        if (formElements.Length != 1) {
            return false;
        }

        IElement formElement = formElements[0];
        List<HtmlFormResult> forms = HtmlParser.ParseFormsWithAngleSharp(html);
        if (forms.Count != 1) {
            return false;
        }

        HtmlFormResult form = forms[0];
        List<KeyValuePair<string, string>> fieldValues = CreateSubmittedFieldValues(formElement);
        Dictionary<string, string> fields = fieldValues
            .GroupBy(static field => field.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
        if (fields.Count == 0) {
            return false;
        }

        int hiddenCount = formElement.QuerySelectorAll("input[type=hidden]")
            .Count(static field => IsSuccessfulControl(field));
        bool mostlyHidden = hiddenCount >= Math.Max(1, fieldValues.Count - 1);
        HtmlFormRelayProtocolHint protocolHint = DetectProtocol(fields.Keys);
        bool hasAutoSubmitMarker = HasAutoSubmitMarker(document, formElement);
        if (!mostlyHidden || (!hasAutoSubmitMarker && protocolHint == HtmlFormRelayProtocolHint.Generic)) {
            return false;
        }

        Uri effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        request = new HtmlFormRelayRequest {
            ActionUri = ResolveAction(form.Metadata.Action, effectiveBaseUri),
            Method = form.Metadata.Method,
            Fields = fields,
            FieldValues = fieldValues,
            FieldNames = fieldValues.Select(static field => field.Key).ToArray(),
            ProtocolHint = protocolHint,
            HasAutoSubmitMarker = hasAutoSubmitMarker
        };
        return true;
    }

    private static Uri ResolveAction(string action, Uri baseUri) {
        if (string.IsNullOrWhiteSpace(action)) {
            return baseUri;
        }

        return Uri.TryCreate(baseUri, action, out Uri? resolved)
            ? resolved
            : new Uri(action, UriKind.Absolute);
    }

    private static Uri GetEffectiveBaseUri(IDocument document, Uri responseUri) {
        string? href = document.QuerySelector("base[href]")?.GetAttribute("href");
        return !string.IsNullOrWhiteSpace(href) && Uri.TryCreate(responseUri, href, out Uri? resolved)
            ? resolved
            : responseUri;
    }

    private static HtmlFormRelayProtocolHint DetectProtocol(IEnumerable<string> fieldNames) {
        HashSet<string> names = new(fieldNames, StringComparer.OrdinalIgnoreCase);
        if (names.Contains("SAMLRequest") || names.Contains("SAMLResponse") || names.Contains("RelayState")) {
            return HtmlFormRelayProtocolHint.Saml;
        }

        if (names.Contains("wa") || names.Contains("wresult") || names.Contains("wctx")) {
            return HtmlFormRelayProtocolHint.WsFederation;
        }

        return HtmlFormRelayProtocolHint.Generic;
    }

    private static List<KeyValuePair<string, string>> CreateSubmittedFieldValues(IElement formElement) {
        List<KeyValuePair<string, string>> fieldValues = new();
        foreach (IElement field in formElement.QuerySelectorAll("input,select,textarea,button")) {
            if (!IsSuccessfulControl(field)) {
                continue;
            }

            string name = field.GetAttribute("name")!;
            if (field.NodeName.Equals("select", StringComparison.OrdinalIgnoreCase) && field.HasAttribute("multiple")) {
                IElement[] selectedOptions = field.QuerySelectorAll("option[selected]").ToArray();
                foreach (IElement option in selectedOptions) {
                    fieldValues.Add(new KeyValuePair<string, string>(name, option.GetAttribute("value") ?? option.TextContent ?? string.Empty));
                }

                continue;
            }

            fieldValues.Add(new KeyValuePair<string, string>(name, HtmlFormFieldUtilities.GetSubmittedValue(field)));
        }

        return fieldValues;
    }

    private static bool IsSuccessfulControl(IElement field) {
        if (field.HasAttribute("disabled") || string.IsNullOrWhiteSpace(field.GetAttribute("name"))) {
            return false;
        }

        string nodeName = field.NodeName;
        string type = field.GetAttribute("type") ?? string.Empty;
        if (nodeName.Equals("button", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (nodeName.Equals("input", StringComparison.OrdinalIgnoreCase)) {
            if (type.Equals("submit", StringComparison.OrdinalIgnoreCase)
                || type.Equals("button", StringComparison.OrdinalIgnoreCase)
                || type.Equals("reset", StringComparison.OrdinalIgnoreCase)
                || type.Equals("image", StringComparison.OrdinalIgnoreCase)
                || type.Equals("file", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if ((type.Equals("checkbox", StringComparison.OrdinalIgnoreCase) || type.Equals("radio", StringComparison.OrdinalIgnoreCase))
                && !field.HasAttribute("checked")) {
                return false;
            }
        }

        return true;
    }

    private static bool HasAutoSubmitMarker(IDocument document, IElement formElement) {
        string formName = formElement.GetAttribute("name") ?? string.Empty;
        string formId = formElement.Id ?? string.Empty;
        return document.QuerySelectorAll("script")
            .Select(static script => script.TextContent ?? string.Empty)
            .Any(script => TargetsFormSubmit(script, formName, formId));
    }

    private static bool TargetsFormSubmit(string script, string formName, string formId) {
        if (Regex.IsMatch(script, @"document\s*\.\s*forms\s*\[\s*0\s*\]\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            return true;
        }

        return MatchesNamedFormSubmit(script, formName) || MatchesIdentifiedFormSubmit(script, formId);
    }

    private static bool MatchesNamedFormSubmit(string script, string formName) {
        if (string.IsNullOrWhiteSpace(formName)) {
            return false;
        }

        string escaped = Regex.Escape(formName);
        return Regex.IsMatch(script, @"document\s*\.\s*forms\s*\[\s*['""]" + escaped + @"['""]\s*\]\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(script, @"document\s*\.\s*forms\s*\.\s*" + escaped + @"\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(script, @"document\s*\.\s*" + escaped + @"\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool MatchesIdentifiedFormSubmit(string script, string formId) {
        if (string.IsNullOrWhiteSpace(formId)) {
            return false;
        }

        string escaped = Regex.Escape(formId);
        return Regex.IsMatch(script, @"document\s*\.\s*getElementById\s*\(\s*['""]" + escaped + @"['""]\s*\)\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(script, @"document\s*\.\s*querySelector\s*\(\s*['""]#" + escaped + @"['""]\s*\)\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
