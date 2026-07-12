using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static void AppendStructuredApiAuthenticationSignals(HtmlCrawlStructuredApiAuthentication authentication, string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        string normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return;
        }

        if (Regex.IsMatch(normalized, @"\b(no authentication required|without authentication|public endpoint|anonymous access)\b", RegexOptions.IgnoreCase)) {
            authentication.Required = false;
            return;
        }

        bool apiKeyNegated = IsStructuredApiKeyNegated(normalized);

        if (ContainsAnyToken(normalized, "authorization")) {
            AppendDistinct(authentication.Headers, "Authorization");
        }
        if (!apiKeyNegated && ContainsAnyToken(normalized, "x-api-key", "api-key", "api key")) {
            AppendDistinct(authentication.Headers, "X-API-Key");
            AppendDistinct(authentication.Schemes, "api-key");
        }
        if (ContainsAnyToken(normalized, "x-auth-token")) {
            AppendDistinct(authentication.Headers, "X-Auth-Token");
            AppendDistinct(authentication.Schemes, "token");
        }
        if (Regex.IsMatch(normalized, @"\bbearer\b|\bjwt\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "bearer");
        }
        if (Regex.IsMatch(normalized, @"\boauth\s*2(?:\.0)?\b|\boauth2\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "oauth2");
        }
        if (Regex.IsMatch(normalized, @"\bbasic auth\b|\bauthorization\s*:\s*basic\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "basic");
            AppendDistinct(authentication.Headers, "Authorization");
        }

        foreach (Match match in Regex.Matches(normalized, @"(?im)^\s*(Authorization|X-API-Key|Api-Key|X-Auth-Token|X-Access-Token)\s*:", RegexOptions.IgnoreCase)) {
            string? header = NormalizeStructuredAuthenticationHeader(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(header)) {
                AppendDistinct(authentication.Headers, header!);
            }
        }

        if (!authentication.Required.HasValue
            && (Regex.IsMatch(normalized, @"\b(auth(?:entication)? required|requires authentication|authenticated requests?|authorization required|include (?:your|an?) api key|provide (?:your|an?) api key|send (?:your|an?) api key|set the authorization header|bearer token required)\b", RegexOptions.IgnoreCase)
                || authentication.Schemes.Count > 0
                || authentication.Headers.Count > 0)) {
            authentication.Required = true;
        }

        if (!authentication.Required.HasValue && apiKeyNegated) {
            authentication.Required = false;
        }
    }

    private static bool IsStructuredApiKeyNegated(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Regex.IsMatch(
            text!,
            @"\b(no api[- ]?key (?:is )?required|api[- ]?key (?:is )?not required|does not require (?:an? )?api[- ]?key|without (?:an? )?api[- ]?key)\b",
            RegexOptions.IgnoreCase);

    private static void RemoveStructuredAuthenticationSignal(IList<string> values, string value) {
        for (int index = values.Count - 1; index >= 0; index--) {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase)) {
                values.RemoveAt(index);
            }
        }
    }

    private static void ApplyStructuredAuthenticationNegations(HtmlCrawlStructuredApiAuthentication authentication) {
        if (!IsStructuredApiKeyNegated(authentication.Summary)) {
            return;
        }

        RemoveStructuredAuthenticationSignal(authentication.Headers, "X-API-Key");
        RemoveStructuredAuthenticationSignal(authentication.Schemes, "api-key");
        if (authentication.Headers.Count == 0 && authentication.Schemes.Count == 0) {
            authentication.Required = false;
        }
    }

    private static string? NormalizeStructuredAuthenticationHeader(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = NormalizeWhitespace(value);
        if (normalized.Equals("authorization", StringComparison.OrdinalIgnoreCase)) {
            return "Authorization";
        }
        if (normalized.Equals("x-api-key", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("api-key", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("api key", StringComparison.OrdinalIgnoreCase)) {
            return "X-API-Key";
        }
        if (normalized.Equals("x-auth-token", StringComparison.OrdinalIgnoreCase)) {
            return "X-Auth-Token";
        }
        if (normalized.Equals("x-access-token", StringComparison.OrdinalIgnoreCase)) {
            return "X-Access-Token";
        }

        return null;
    }
}
