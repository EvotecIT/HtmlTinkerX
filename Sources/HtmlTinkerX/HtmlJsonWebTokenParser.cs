using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HtmlTinkerX;

/// <summary>
/// Decodes JSON Web Tokens into safe, operator-friendly summaries.
/// </summary>
public static class HtmlJsonWebTokenParser {
    private static readonly long MinUnixTimeSeconds = DateTimeOffset.MinValue.ToUnixTimeSeconds();
    private static readonly long MaxUnixTimeSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static readonly string[] SensitiveClaimNames = {
        "email",
        "family_name",
        "given_name",
        "ipaddr",
        "name",
        "nonce",
        "oid",
        "preferred_username",
        "sid",
        "sub",
        "unique_name",
        "upn",
        "uti"
    };

    /// <summary>
    /// Decodes and summarizes a compact JWT value.
    /// </summary>
    /// <param name="token">Raw compact JSON Web Token.</param>
    /// <param name="includeSensitiveValues">Reveal subject and user-identifying claim values.</param>
    /// <param name="includeJson">Include decoded header and payload JSON.</param>
    /// <returns>Safe JWT summary.</returns>
    public static HtmlJsonWebTokenSummary Parse(string token, bool includeSensitiveValues = false, bool includeJson = false) {
        HtmlJsonWebTokenSummary summary = new() {
            SuggestedCommand = "ConvertFrom-HtmlJsonWebToken -Token $handoff.FormData['id_token']"
        };

        if (string.IsNullOrWhiteSpace(token)) {
            summary.ErrorMessage = "JSON Web Token value is empty.";
            return summary;
        }

        if (string.Equals(token.Trim(), "<redacted>", StringComparison.OrdinalIgnoreCase)) {
            summary.ErrorMessage = "JSON Web Token value is redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before analyzing it.";
            summary.SuggestedCommand = "Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlJsonWebToken";
            return summary;
        }

        string[] parts = token.Trim().Split('.');
        if (parts.Length < 2 || parts.Length > 3) {
            summary.ErrorMessage = "JSON Web Token must have two or three base64url segments.";
            return summary;
        }

        if (!TryDecodeJsonSegment(parts[0], out JsonDocument? header, out string headerJson, out string headerError)) {
            summary.ErrorMessage = $"JWT header could not be decoded: {headerError}";
            return summary;
        }

        using (header) {
            if (!TryDecodeJsonSegment(parts[1], out JsonDocument? payload, out string payloadJson, out string payloadError)) {
                summary.ErrorMessage = $"JWT payload could not be decoded: {payloadError}";
                return summary;
            }

            using (payload) {
                if (header!.RootElement.ValueKind != JsonValueKind.Object) {
                    summary.ErrorMessage = "JWT header must decode to a JSON object.";
                    return summary;
                }

                if (payload!.RootElement.ValueKind != JsonValueKind.Object) {
                    summary.ErrorMessage = "JWT payload must decode to a JSON object.";
                    return summary;
                }

                PopulateHeader(summary, header!.RootElement);
                string? payloadValidationError = PopulatePayload(summary, payload!.RootElement, includeSensitiveValues);
                if (payloadValidationError != null) {
                    summary.ErrorMessage = payloadValidationError;
                    return summary;
                }

                if (includeJson) {
                    summary.HeaderJson = headerJson;
                    summary.PayloadJson = includeSensitiveValues ? payloadJson : RedactPayloadJson(payload.RootElement);
                }
            }
        }

        summary.IsValid = true;
        summary.SuggestedCommand = "Format-List Issuer,Audiences,Expires,NotBefore,IssuedAt,Scopes,Warnings";
        AddWarnings(summary, parts, includeSensitiveValues, includeJson);
        return summary;
    }

    private static void PopulateHeader(HtmlJsonWebTokenSummary summary, JsonElement header) {
        summary.Algorithm = GetJsonString(header, "alg");
        summary.Type = GetJsonString(header, "typ");
        summary.KeyId = GetJsonString(header, "kid");
    }

    private static string? PopulatePayload(HtmlJsonWebTokenSummary summary, JsonElement payload, bool includeSensitiveValues) {
        summary.Issuer = GetJsonString(payload, "iss");
        summary.Subject = GetClaimValue(payload, "sub", includeSensitiveValues, summary);
        summary.TenantId = FirstNonEmpty(GetJsonString(payload, "tid"), GetJsonString(payload, "tenant"));
        summary.ClientId = FirstNonEmpty(GetJsonString(payload, "azp"), GetJsonString(payload, "appid"), GetJsonString(payload, "client_id"), GetJsonString(payload, "aud"));
        if (!TryGetUnixTime(payload, "exp", out DateTimeOffset? expires, out string? errorMessage)) {
            return errorMessage;
        }

        if (!TryGetUnixTime(payload, "nbf", out DateTimeOffset? notBefore, out errorMessage)) {
            return errorMessage;
        }

        if (!TryGetUnixTime(payload, "iat", out DateTimeOffset? issuedAt, out errorMessage)) {
            return errorMessage;
        }

        summary.Expires = expires;
        summary.NotBefore = notBefore;
        summary.IssuedAt = issuedAt;
        summary.Audiences.AddRange(GetClaimStringValues(payload, "aud"));
        summary.Scopes.AddRange(GetScopeValues(payload));

        foreach (JsonProperty property in payload.EnumerateObject().OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)) {
            bool sensitive = IsSensitiveClaimName(property.Name);
            string value = sensitive && !includeSensitiveValues
                ? "<redacted>"
                : FormatJsonValue(property.Value);
            if (sensitive) {
                summary.ContainsSensitiveValues = true;
            }

            summary.Claims.Add(new HtmlJsonWebTokenClaim {
                Name = property.Name,
                Value = value,
                ValueKind = property.Value.ValueKind.ToString(),
                Redacted = sensitive && !includeSensitiveValues
            });
        }

        return null;
    }

    private static void AddWarnings(HtmlJsonWebTokenSummary summary, string[] parts, bool includeSensitiveValues, bool includeJson) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (summary.Expires.HasValue && summary.Expires.Value <= now) {
            summary.Warnings.Add("JSON Web Token is expired.");
        }

        if (summary.NotBefore.HasValue && summary.NotBefore.Value > now) {
            summary.Warnings.Add("JSON Web Token is not valid yet.");
        }

        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2])) {
            summary.Warnings.Add("JWT has no signature segment.");
        }

        if (string.Equals(summary.Algorithm, "none", StringComparison.OrdinalIgnoreCase)) {
            summary.Warnings.Add("JWT header uses alg=none.");
        }

        summary.Warnings.Add("JWT signature and issuer keys were not verified. Treat this as decoding and triage only.");

        if (summary.ContainsSensitiveValues && !includeSensitiveValues) {
            summary.Warnings.Add("Subject and user-identifying claim values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.");
        }

        if (includeJson && includeSensitiveValues) {
            summary.Warnings.Add("Decoded payload JSON includes sensitive claim values. Avoid storing it in logs or transcripts.");
        }
    }

    private static string GetClaimValue(JsonElement payload, string name, bool includeSensitiveValues, HtmlJsonWebTokenSummary summary) {
        if (!payload.TryGetProperty(name, out JsonElement value)) {
            return string.Empty;
        }

        if (IsSensitiveClaimName(name)) {
            summary.ContainsSensitiveValues = true;
            return includeSensitiveValues ? FormatJsonValue(value) : "<redacted>";
        }

        return FormatJsonValue(value);
    }

    private static IReadOnlyList<string> GetScopeValues(JsonElement payload) {
        List<string> values = new();
        foreach (string claimName in new[] { "scp", "scope", "roles" }) {
            foreach (string value in GetClaimStringValues(payload, claimName)) {
                foreach (string part in value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (!values.Contains(part, StringComparer.OrdinalIgnoreCase)) {
                        values.Add(part);
                    }
                }
            }
        }

        return values;
    }

    private static IReadOnlyList<string> GetClaimStringValues(JsonElement payload, string name) {
        if (!payload.TryGetProperty(name, out JsonElement value)) {
            return Array.Empty<string>();
        }

        if (value.ValueKind == JsonValueKind.Array) {
            return value.EnumerateArray()
                .Select(FormatJsonValue)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        string single = FormatJsonValue(value);
        return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single };
    }

    private static string RedactPayloadJson(JsonElement payload) {
        using JsonDocument clone = JsonDocument.Parse(payload.GetRawText());
        Dictionary<string, object?> redacted = new(StringComparer.Ordinal);
        foreach (JsonProperty property in clone.RootElement.EnumerateObject()) {
            redacted[property.Name] = IsSensitiveClaimName(property.Name)
                ? "<redacted>"
                : ConvertJsonElement(property.Value);
        }

        return JsonSerializer.Serialize(redacted, new JsonSerializerOptions {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        });
    }

    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out double doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value), StringComparer.Ordinal),
            _ => null
        };

    private static bool TryDecodeJsonSegment(string segment, out JsonDocument? document, out string json, out string errorMessage) {
        document = null;
        json = string.Empty;
        errorMessage = string.Empty;
        try {
            byte[] bytes = DecodeBase64Url(segment);
            json = Encoding.UTF8.GetString(bytes);
            document = JsonDocument.Parse(json);
            return true;
        } catch (Exception ex) when (ex is FormatException || ex is JsonException || ex is DecoderFallbackException || ex is ArgumentException) {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static byte[] DecodeBase64Url(string value) {
        string padded = value.Replace('-', '+').Replace('_', '/');
        int remainder = padded.Length % 4;
        if (remainder == 2) {
            padded += "==";
        } else if (remainder == 3) {
            padded += "=";
        } else if (remainder == 1) {
            throw new FormatException("Invalid base64url length.");
        }

        return Convert.FromBase64String(padded);
    }

    private static bool TryGetUnixTime(JsonElement payload, string name, out DateTimeOffset? value, out string? errorMessage) {
        value = null;
        errorMessage = null;
        if (!payload.TryGetProperty(name, out JsonElement element)) {
            return true;
        }

        long seconds;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out seconds)
            || element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out seconds)) {
            if (seconds < MinUnixTimeSeconds || seconds > MaxUnixTimeSeconds) {
                errorMessage = $"JWT claim '{name}' is outside the supported Unix time range.";
                return false;
            }

            value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return true;
    }

    private static string GetJsonString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? FormatJsonValue(value) : string.Empty;

    private static string FormatJsonValue(JsonElement element) =>
        element.ValueKind switch {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(FormatJsonValue)),
            JsonValueKind.Object => element.GetRawText(),
            _ => string.Empty
        };

    private static bool IsSensitiveClaimName(string name) =>
        SensitiveClaimNames.Contains(name, StringComparer.OrdinalIgnoreCase)
        || HtmlSensitiveValueRedactor.IsSensitiveName(name);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
