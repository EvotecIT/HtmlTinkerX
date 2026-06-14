using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

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
        Dictionary<string, string> fields = form.Fields
            .Where(static field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last().Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        if (fields.Count == 0) {
            return false;
        }

        int hiddenCount = form.Fields.Count(static field => field.Type == HtmlFormFieldType.Hidden);
        bool mostlyHidden = hiddenCount >= Math.Max(1, fields.Count - 1);
        HtmlFormRelayProtocolHint protocolHint = DetectProtocol(fields.Keys);
        bool hasAutoSubmitMarker = HasAutoSubmitMarker(document, formElement);
        if (!mostlyHidden || (!hasAutoSubmitMarker && protocolHint == HtmlFormRelayProtocolHint.Generic)) {
            return false;
        }

        request = new HtmlFormRelayRequest {
            ActionUri = ResolveAction(form.Metadata.Action, baseUri),
            Method = form.Metadata.Method,
            Fields = fields,
            FieldNames = fields.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
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

    private static bool HasAutoSubmitMarker(IDocument document, IElement formElement) {
        string formName = formElement.GetAttribute("name") ?? string.Empty;
        string formId = formElement.Id ?? string.Empty;
        return document.QuerySelectorAll("script")
            .Select(static script => script.TextContent ?? string.Empty)
            .Any(script =>
                script.IndexOf(".submit()", StringComparison.OrdinalIgnoreCase) >= 0
                || script.IndexOf("document.forms[0]", StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrWhiteSpace(formName) && script.IndexOf(formName, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(formId) && script.IndexOf(formId, StringComparison.OrdinalIgnoreCase) >= 0));
    }
}
