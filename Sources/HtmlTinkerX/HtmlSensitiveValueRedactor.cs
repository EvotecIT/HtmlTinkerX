using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

internal static class HtmlSensitiveValueRedactor {
    internal static readonly string[] SensitiveNames = {
        "access_token",
        "api_key",
        "apikey",
        "auth",
        "code",
        "credential",
        "csrf",
        "key",
        "password",
        "refresh_token",
        "secret",
        "session",
        "token"
    };

    internal static bool HasSensitiveQuery(Uri? uri) {
        if (uri == null) {
            return false;
        }

        return !string.IsNullOrWhiteSpace(uri.UserInfo)
            || HasSensitiveQueryText(uri.Query)
            || HasSensitiveQueryText(uri.Fragment);
    }

    internal static bool HasSensitiveQueryText(string value) {
        return HasSensitiveQueryText(value, 0);
    }

    private static bool HasSensitiveQueryText(string value, int depth) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        if (!string.Equals(value, RedactUserInfo(value), StringComparison.Ordinal)) {
            return true;
        }

        foreach (string parameters in GetParameterSegments(value)) {
            foreach (string pair in parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
                string[] keyValue = pair.Split(new[] { '=' }, 2);
                string name = keyValue[0];
                if (IsSensitiveName(SafeUnescapeDataString(name))) {
                    return true;
                }

                if (keyValue.Length == 2 && depth < 4 && HasSensitiveNestedValue(keyValue[1], depth + 1)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasSensitiveNestedValue(string value, int depth) {
        try {
            return HasSensitiveQueryText(Uri.UnescapeDataString(value), depth);
        } catch (UriFormatException) {
            return false;
        }
    }

    internal static string RedactSensitiveQueryValues(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string redactedValue = RedactUserInfo(value);
        int queryIndex = redactedValue.IndexOf('?');
        int fragmentIndex = redactedValue.IndexOf('#');
        if (queryIndex < 0 && fragmentIndex < 0) {
            return redactedValue;
        }

        if (queryIndex >= 0 && (fragmentIndex < 0 || queryIndex < fragmentIndex)) {
            int queryEnd = fragmentIndex >= 0 ? fragmentIndex : redactedValue.Length;
            string prefix = redactedValue.Substring(0, queryIndex + 1);
            string query = redactedValue.Substring(queryIndex + 1, queryEnd - queryIndex - 1);
            string fragment = fragmentIndex >= 0 ? "#" + RedactParameterPairs(redactedValue.Substring(fragmentIndex + 1)) : string.Empty;
            return prefix + RedactParameterPairs(query) + fragment;
        }

        return redactedValue.Substring(0, fragmentIndex + 1) + RedactParameterPairs(redactedValue.Substring(fragmentIndex + 1));
    }

    private static string[] GetParameterSegments(string value) {
        int queryIndex = value.IndexOf('?');
        int fragmentIndex = value.IndexOf('#');
        if (queryIndex < 0 && fragmentIndex < 0) {
            return Array.Empty<string>();
        }

        if (queryIndex >= 0 && (fragmentIndex < 0 || queryIndex < fragmentIndex)) {
            int queryEnd = fragmentIndex >= 0 ? fragmentIndex : value.Length;
            string query = value.Substring(queryIndex + 1, queryEnd - queryIndex - 1);
            if (fragmentIndex >= 0) {
                string fragment = value.Substring(fragmentIndex + 1);
                return HasParameterPairs(fragment) ? new[] { query, fragment } : new[] { query };
            }

            return new[] { query };
        }

        string fragmentOnly = value.Substring(fragmentIndex + 1);
        return HasParameterPairs(fragmentOnly) ? new[] { fragmentOnly } : Array.Empty<string>();
    }

    private static string RedactParameterPairs(string parameters) {
        string[] pairs = parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        string[] redactedPairs = pairs.Select(pair => {
            string[] keyValue = pair.Split(new[] { '=' }, 2);
            string name = SafeUnescapeDataString(keyValue[0]);
            if (IsSensitiveName(name)) {
                return keyValue[0] + "=<redacted>";
            }

            if (keyValue.Length == 2 && TryRedactNestedValue(keyValue[1], out string? nestedRedacted)) {
                return keyValue[0] + "=" + nestedRedacted;
            }

            return pair;
        }).ToArray();

        return string.Join("&", redactedPairs);
    }

    private static bool HasParameterPairs(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.IndexOf('=') >= 0;

    private static string SafeUnescapeDataString(string value) {
        try {
            return Uri.UnescapeDataString(value);
        } catch (UriFormatException) {
            return value;
        }
    }

    private static bool TryRedactNestedValue(string value, out string redacted) {
        redacted = value;
        string decoded;
        try {
            decoded = Uri.UnescapeDataString(value);
        } catch (UriFormatException) {
            return false;
        }

        if (string.Equals(decoded, value, StringComparison.Ordinal) && !HasSensitiveQueryText(decoded)) {
            return false;
        }

        if (!HasSensitiveQueryText(decoded)) {
            return false;
        }

        string nestedRedacted = RedactSensitiveQueryValues(decoded);
        if (string.Equals(nestedRedacted, decoded, StringComparison.Ordinal)) {
            return false;
        }

        redacted = string.Equals(decoded, value, StringComparison.Ordinal)
            ? nestedRedacted
            : Uri.EscapeDataString(nestedRedacted);
        return true;
    }

    private static string RedactUserInfo(string value) =>
        Regex.Replace(
            value,
            "^(([A-Za-z][A-Za-z0-9+.-]*:)?//)([^/?#@]+@)",
            "$1<redacted>@",
            RegexOptions.CultureInvariant);

    internal static bool IsSensitiveName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && SensitiveNames.Any(name => value.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

    internal static string RedactSensitiveStructuredText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string redacted = Regex.Replace(
            value,
            "(\"[^\"]*(?:access_token|api_key|apikey|auth|code|credential|csrf|key|password|refresh_token|secret|session|token)[^\"]*\"\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "([A-Za-z_$][A-Za-z0-9_$]*(?:access_token|api_key|apikey|auth|code|credential|csrf|key|password|refresh_token|secret|session|token)[A-Za-z0-9_$]*\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'",
            RedactSensitiveUrlLiteral,
            RegexOptions.CultureInvariant);

        return redacted;
    }

    private static string RedactSensitiveUrlLiteral(Match match) {
        string literal = match.Value;
        if (literal.Length < 2) {
            return literal;
        }

        char quote = literal[0];
        string value = literal.Substring(1, literal.Length - 2);
        string normalizedValue = NormalizeEscapedUrlLiteral(value);
        if (!HasSensitiveQueryText(normalizedValue) && string.Equals(normalizedValue, RedactUserInfo(normalizedValue), StringComparison.Ordinal)) {
            return literal;
        }

        string redacted = RedactSensitiveQueryValues(normalizedValue);
        return quote + redacted.Replace("\\", "\\\\").Replace(quote.ToString(), "\\" + quote) + quote;
    }

    private static string NormalizeEscapedUrlLiteral(string value) {
        string normalized = value.Replace("\\/", "/").Replace("&amp;", "&");
        return Regex.Replace(
            normalized,
            @"\\u00(23|26|3[dD]|3[fF])",
            match => match.Groups[1].Value.ToUpperInvariant() switch {
                "23" => "#",
                "26" => "&",
                "3D" => "=",
                "3F" => "?",
                _ => match.Value
            },
            RegexOptions.CultureInvariant);
    }
}
