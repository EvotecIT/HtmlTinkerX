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
        if (formElements.Length == 0) {
            return false;
        }

        List<HtmlFormResult> forms = HtmlParserFromForm.ParseFormsDocument(document, baseUri, baseUri);
        var controlsByForm = HtmlFormControlUtilities.GetControlsByForm(document);
        if (forms.Count != formElements.Length) {
            return false;
        }

        for (int formIndex = 0; formIndex < formElements.Length; formIndex++) {
            IElement formElement = formElements[formIndex];
            HtmlFormResult form = forms[formIndex];
            List<IElement> controls = controlsByForm.TryGetValue(formElement, out List<IElement>? associatedControls)
                ? associatedControls : new List<IElement>();
            List<IElement> successfulControls = HtmlFormControlUtilities.GetSuccessfulControls(controls);
            List<KeyValuePair<string, string>> fieldValues = form.SuccessfulFields;
            Dictionary<string, string> fields = fieldValues
                .GroupBy(static field => field.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
            if (fields.Count == 0) {
                continue;
            }

            int hiddenCount = successfulControls.Count(static field =>
                field.NodeName.Equals("input", StringComparison.OrdinalIgnoreCase)
                && (field.GetAttribute("type") ?? string.Empty).Equals("hidden", StringComparison.OrdinalIgnoreCase));
            bool mostlyHidden = hiddenCount >= Math.Max(1, fieldValues.Count - 1);
            HtmlFormRelayProtocolHint protocolHint = DetectProtocol(fields.Keys);
            bool hasAutoSubmitMarker = HasAutoSubmitMarker(document, formElement, formIndex);
            if (!mostlyHidden || !hasAutoSubmitMarker) {
                continue;
            }

            Uri effectiveBaseUri = string.IsNullOrWhiteSpace(form.Metadata.Action)
                ? baseUri
                : HtmlFormUrlUtilities.GetEffectiveBaseUri(document, baseUri);
            if (!HtmlFormUrlUtilities.TryResolveAction(form.Metadata.Action, effectiveBaseUri, out Uri? actionUri)) {
                continue;
            }

            request = new HtmlFormRelayRequest {
                ActionUri = actionUri,
                Method = form.Metadata.Method,
                Fields = fields,
                FieldValues = fieldValues,
                FieldNames = fieldValues.Select(static field => field.Key).ToArray(),
                ProtocolHint = protocolHint,
                HasAutoSubmitMarker = hasAutoSubmitMarker
            };
            return true;
        }

        return false;
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

    private static bool HasAutoSubmitMarker(IDocument document, IElement formElement, int formIndex) {
        string formName = formElement.GetAttribute("name") ?? string.Empty;
        string formId = formElement.Id ?? string.Empty;
        return document.QuerySelectorAll("script")
            .Where(IsExecutableScriptElement)
            .Select(static script => script.TextContent ?? string.Empty)
            .Any(script => TargetsFormSubmit(script, formName, formId, formIndex))
            || document.All
                .SelectMany(static element => element.Attributes)
                .Where(static attribute => IsAutomaticSubmitEventAttribute(attribute.Name))
                .Any(attribute => TargetsFormSubmit(attribute.Value ?? string.Empty, formName, formId, formIndex));
    }

    private static bool IsAutomaticSubmitEventAttribute(string attributeName) =>
        attributeName.Equals("onload", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("onpageshow", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("onreadystatechange", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableScriptElement(IElement script) {
        return HtmlJavaScriptVariableSelector.IsJavaScriptScriptType(script.GetAttribute("type"));
    }

    private static bool TargetsFormSubmit(string script, string formName, string formId, int formIndex) {
        if (string.IsNullOrWhiteSpace(script)) {
            return false;
        }

        if (!ContainsTargetedFormSubmit(script, formName, formId, formIndex)) {
            return false;
        }

        string scriptWithoutUserDrivenHandlers = RemoveUserDrivenSubmitContexts(script);
        if (HasAutomaticSubmitWrapper(scriptWithoutUserDrivenHandlers, formName, formId, formIndex)) {
            return true;
        }

        if (HasUserDrivenSubmitContext(script)) {
            return false;
        }

        return ContainsTargetedFormSubmit(RemoveFunctionDeclarationBodies(scriptWithoutUserDrivenHandlers), formName, formId, formIndex);
    }

    private static bool ContainsTargetedFormSubmit(string script, string formName, string formId, int formIndex) {
        return Regex.IsMatch(script, @"document\s*\.\s*forms\s*\[\s*" + formIndex + @"\s*\]\s*\.\s*submit\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || MatchesNamedFormSubmit(script, formName)
            || MatchesIdentifiedFormSubmit(script, formId);
    }

    private static bool HasAutomaticSubmitWrapper(string script, string formName, string formId, int formIndex) {
        string[] automaticMarkers = {
            @"setTimeout\s*\(",
            @"setInterval\s*\(",
            @"requestAnimationFrame\s*\(",
            @"(?:window|document)\s*\.\s*(?:onload|onpageshow|onreadystatechange)\s*=",
            @"addEventListener\s*\(\s*['""](?:load|pageshow|DOMContentLoaded|readystatechange)['""]"
        };

        return automaticMarkers.Any(marker =>
            Regex.IsMatch(script, marker, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && ContainsTargetedFormSubmit(script, formName, formId, formIndex));
    }

    private static bool HasUserDrivenSubmitContext(string script) {
        const string userEvents = "click|dblclick|auxclick|submit|change|input|keydown|keyup|keypress|mousedown|mouseup|pointerdown|pointerup|touchstart|touchend";
        return Regex.IsMatch(
            script,
            @"addEventListener\s*\(\s*['""](?:" + userEvents + @")['""].*?submit\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    private static string RemoveUserDrivenSubmitContexts(string script) {
        const string userEvents = "click|dblclick|auxclick|submit|change|input|keydown|keyup|keypress|mousedown|mouseup|pointerdown|pointerup|touchstart|touchend";
        return Regex.Replace(
            script,
            @"addEventListener\s*\(\s*['""](?:" + userEvents + @")['""].*?submit\s*\(.*?\)\s*\)\s*;?",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    private static string RemoveFunctionDeclarationBodies(string script) {
        return Regex.Replace(
            script,
            @"function(?:\s+[A-Za-z_$][A-Za-z0-9_$]*)?\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
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
