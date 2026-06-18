using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Protocol-aware analysis helpers for captured SSO handoff forms.
/// </summary>
public static class HtmlSsoHandoffAnalyzer {
    private static readonly string[] JwtFieldNames = {
        "id_token",
        "access_token"
    };

    /// <summary>
    /// Analyzes a captured SSO handoff form and routes known protocol artifacts to safe decoders.
    /// </summary>
    /// <param name="handoff">Captured SSO handoff returned by browser inspection.</param>
    /// <param name="includeSensitiveValues">Reveal sensitive values in nested summaries.</param>
    /// <param name="includeXml">Include decoded SAML XML when a SAMLResponse is present.</param>
    /// <param name="includeJson">Include decoded JWT JSON when id_token or access_token fields are present.</param>
    /// <returns>Safe protocol-aware handoff analysis.</returns>
    public static HtmlSsoHandoffAnalysis Analyze(
        HtmlBrowserSsoHandoff handoff,
        bool includeSensitiveValues = false,
        bool includeXml = false,
        bool includeJson = false) {
        if (handoff == null) {
            throw new ArgumentNullException(nameof(handoff));
        }

        HtmlSsoHandoffAnalysis analysis = new() {
            Kind = handoff.Kind,
            PageUrl = handoff.PageUrl,
            Title = handoff.Title,
            Action = includeSensitiveValues ? handoff.Action : HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(handoff.Action),
            Method = handoff.Method,
            ContainsRedactedValues = handoff.Fields.Any(field => field.Redacted || (!includeSensitiveValues && HtmlSensitiveValueRedactor.IsSensitiveName(field.Name))),
            ContainsTruncatedValues = handoff.Fields.Any(static field => field.Truncated)
        };

        foreach (string name in GetFieldNames(handoff)) {
            analysis.FieldNames.Add(name);
        }

        string samlResponse = GetFieldValue(handoff, "SAMLResponse");
        if (!string.IsNullOrWhiteSpace(samlResponse)) {
            analysis.SamlResponse = HtmlSamlResponseParser.Parse(samlResponse, includeSensitiveValues, includeXml);
            analysis.HasProtocolArtifact = true;
        }

        foreach (string fieldName in JwtFieldNames) {
            string token = GetFieldValue(handoff, fieldName);
            if (string.IsNullOrWhiteSpace(token)) {
                continue;
            }

            analysis.JsonWebTokens.Add(new HtmlSsoHandoffTokenSummary {
                FieldName = fieldName,
                Summary = HtmlJsonWebTokenParser.Parse(token, includeSensitiveValues, includeJson)
            });
            analysis.HasProtocolArtifact = true;
        }

        analysis.AuthorizationCodePresent = !string.IsNullOrWhiteSpace(GetFieldValue(handoff, "code"));
        analysis.StatePresent = !string.IsNullOrWhiteSpace(GetFieldValue(handoff, "state"))
            || !string.IsNullOrWhiteSpace(GetFieldValue(handoff, "RelayState"));
        analysis.Error = GetFieldValue(handoff, "error");
        analysis.ErrorDescription = GetAnalysisFieldValue(handoff, "error_description", includeSensitiveValues);
        if (analysis.AuthorizationCodePresent) {
            analysis.HasProtocolArtifact = true;
        }
        if (!string.IsNullOrWhiteSpace(analysis.Error)) {
            analysis.HasProtocolArtifact = true;
        }

        AddWarnings(analysis);
        analysis.SuggestedCommand = analysis.ContainsRedactedValues
            ? "Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSsoHandoff"
            : "$analysis | Format-List Kind,Action,FieldNames,Warnings; $analysis.SamlResponse; $analysis.JsonWebTokens";
        return analysis;
    }

    private static void AddWarnings(HtmlSsoHandoffAnalysis analysis) {
        if (analysis.ContainsRedactedValues) {
            analysis.Warnings.Add("One or more handoff values are redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before deep protocol analysis or replay.");
        }

        if (analysis.ContainsTruncatedValues) {
            analysis.Warnings.Add("One or more handoff values are truncated. Increase -MaxValueLength or set it to 0 before analysis or replay.");
        }

        if (analysis.AuthorizationCodePresent) {
            analysis.Warnings.Add("OAuth authorization code is present. Codes are short-lived and cannot be decoded locally; exchange only through the intended authorized client flow.");
        }

        if (!string.IsNullOrWhiteSpace(analysis.Error)) {
            string description = string.IsNullOrWhiteSpace(analysis.ErrorDescription)
                ? string.Empty
                : $" ({analysis.ErrorDescription})";
            analysis.Warnings.Add($"OAuth/OpenID Connect error returned: {analysis.Error}{description}.");
        }

        if (analysis.SamlResponse?.Warnings.Count > 0) {
            analysis.Warnings.AddRange(analysis.SamlResponse.Warnings.Select(static warning => $"SAML: {warning}"));
        }

        foreach (HtmlSsoHandoffTokenSummary token in analysis.JsonWebTokens) {
            analysis.Warnings.AddRange(token.Summary.Warnings.Select(warning => $"{token.FieldName}: {warning}"));
        }

        if (!analysis.HasProtocolArtifact) {
            analysis.Warnings.Add("No SAMLResponse, id_token, access_token, or authorization code field was found.");
        }
    }

    private static IReadOnlyList<string> GetFieldNames(HtmlBrowserSsoHandoff handoff) {
        SortedSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlBrowserSsoField field in handoff.Fields) {
            if (!string.IsNullOrWhiteSpace(field.Name)) {
                names.Add(field.Name);
            }
        }

        foreach (string name in handoff.FormData.Keys) {
            if (!string.IsNullOrWhiteSpace(name)) {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    private static string GetFieldValue(HtmlBrowserSsoHandoff handoff, string fieldName) {
        if (handoff.FormData.TryGetValue(fieldName, out string? value)) {
            return value ?? string.Empty;
        }

        string canonicalFieldName = NormalizeSsoFieldName(fieldName);
        foreach (KeyValuePair<string, string> item in handoff.FormData) {
            if (string.Equals(NormalizeSsoFieldName(item.Key), canonicalFieldName, StringComparison.OrdinalIgnoreCase)) {
                return item.Value ?? string.Empty;
            }
        }

        HtmlBrowserSsoField? field = handoff.Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field != null) {
            return field.Value ?? string.Empty;
        }

        field = handoff.Fields.FirstOrDefault(field => string.Equals(NormalizeSsoFieldName(field.Name), canonicalFieldName, StringComparison.OrdinalIgnoreCase));
        return field?.Value ?? string.Empty;
    }

    private static string GetAnalysisFieldValue(HtmlBrowserSsoHandoff handoff, string fieldName, bool includeSensitiveValues) {
        string value = GetFieldValue(handoff, fieldName);
        if (includeSensitiveValues || string.IsNullOrWhiteSpace(value) || string.Equals(value, "<redacted>", StringComparison.OrdinalIgnoreCase)) {
            return value;
        }

        return HtmlSensitiveValueRedactor.IsSensitiveName(fieldName) ? "<redacted>" : HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(value);
    }

    private static string NormalizeSsoFieldName(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return string.Empty;
        }

        return new string(name
            .Where(static c => char.IsLetterOrDigit(c))
            .Select(static c => char.ToLowerInvariant(c))
            .ToArray());
    }
}
