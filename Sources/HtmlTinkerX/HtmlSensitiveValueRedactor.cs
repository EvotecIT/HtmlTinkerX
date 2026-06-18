using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

internal static class HtmlSensitiveValueRedactor {
    private const string StructuredSensitiveNamePattern = "access_token|api_key|apikey|auth|code|credential|csrf|key|mfa|otp|passcode|password|pin|pwd|refresh_token|relaystate|samlrequest|samlresponse|secret|session|token|wctx|wresult";

    internal static readonly string[] SensitiveNames = {
        "access_token",
        "api_key",
        "apikey",
        "auth",
        "code",
        "credential",
        "csrf",
        "error",
        "error_description",
        "error_uri",
        "id_token",
        "key",
        "mfa",
        "otp",
        "password",
        "passcode",
        "pin",
        "pwd",
        "refresh_token",
        "relaystate",
        "samlrequest",
        "samlresponse",
        "secret",
        "session",
        "session_state",
        "state",
        "token",
        "wctx",
        "wresult"
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
                string suffix = keyValue.Length == 2 ? GetTrailingParameterValueDelimiter(keyValue[1]) : string.Empty;
                return keyValue[0] + "=<redacted>" + suffix;
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

    private static string GetTrailingParameterValueDelimiter(string value) {
        const string RedactedMarker = "<redacted>";
        if (value.StartsWith(RedactedMarker, StringComparison.OrdinalIgnoreCase)) {
            return value.Substring(RedactedMarker.Length);
        }

        int index = value.Length;
        while (index > 0 && IsParameterValueDelimiter(value[index - 1])) {
            index--;
        }

        return index == value.Length ? string.Empty : value.Substring(index);
    }

    private static bool IsParameterValueDelimiter(char value) =>
        char.IsWhiteSpace(value)
        || value == '\''
        || value == '"'
        || value == ']'
        || value == ')'
        || value == '}'
        || value == '>';

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

    internal static bool IsSensitiveName(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        if (SensitiveNames.Any(name => IsSensitiveNameMatch(value, name))) {
            return true;
        }

        return Regex.IsMatch(value, @"access%[A-Za-z0-9]*token", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsSensitiveNameMatch(string value, string sensitiveName) {
        int startIndex = 0;
        while (startIndex < value.Length) {
            int index = value.IndexOf(sensitiveName, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0) {
                return false;
            }

            if (HasNameBoundaryBefore(value, index) && HasNameBoundaryAfter(value, index + sensitiveName.Length - 1)) {
                return true;
            }

            startIndex = index + 1;
        }

        return false;
    }

    private static bool HasNameBoundaryBefore(string value, int index) {
        if (index <= 0) {
            return true;
        }

        char previous = value[index - 1];
        char current = value[index];
        return !char.IsLetterOrDigit(previous)
            || (char.IsLower(previous) && char.IsUpper(current));
    }

    private static bool HasNameBoundaryAfter(string value, int index) {
        if (index >= value.Length - 1) {
            return true;
        }

        char current = value[index];
        char next = value[index + 1];
        return !char.IsLetterOrDigit(next)
            || (char.IsLower(current) && char.IsUpper(next));
    }

    internal static string RedactSensitiveStructuredText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string redacted = Regex.Replace(
            value,
            "([A-Za-z_$][A-Za-z0-9_$.]*(?:" + StructuredSensitiveNamePattern + ")[A-Za-z0-9_$]*\\s*=\\s*)(\\{[^;]*\\}|\\\"(?:\\\\.|[^\\\"])*\\\"|'(?:\\\\.|[^'])*'|[^;\\r\\n]+)",
            "$1<redacted>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "((?:\"[^\"]*(?:" + StructuredSensitiveNamePattern + ")[^\"]*\"|'[^']*(?:" + StructuredSensitiveNamePattern + ")[^']*')\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "([A-Za-z_$][A-Za-z0-9_$]*(?:" + StructuredSensitiveNamePattern + ")[A-Za-z0-9_$]*\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "((?:[A-Za-z_$][A-Za-z0-9_$]*\\.)?\\bstate\\s*=\\s*)(\\{[^;]*\\}|\\\"(?:\\\\.|[^\\\"])*\\\"|'(?:\\\\.|[^'])*'|[^;\\r\\n]+)",
            "$1<redacted>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "((?:\"state\"|'state')\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "(\\bstate\\s*:\\s*)(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^,}\\]]+)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            "\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'",
            RedactSensitiveUrlLiteral,
            RegexOptions.CultureInvariant);

        return redacted;
    }

    internal static string RedactSensitiveEvidenceText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string redacted = RedactSensitiveStructuredText(value);
        redacted = Regex.Replace(
            redacted,
            @"(?i)(bearer\s+)[A-Za-z0-9._~+/\-]+=*",
            "$1<redacted>",
            RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            @"(?i)((?:access[_-]?token|api[_-]?key|code|mfa|otp|passcode|password|pin|pwd|secret|refresh[_-]?token|session[_-]?id|saml(?:request|response)|state|relaystate|wresult|wctx)\s*=\s*)[^\s&<>""']+",
            "$1<redacted>",
            RegexOptions.CultureInvariant);

        redacted = Regex.Replace(
            redacted,
            @"<input\b[^>]*>",
            RedactSensitiveInputElement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return redacted;
    }

    private static string RedactSensitiveInputElement(Match match) {
        string tag = match.Value;
        if (!Regex.IsMatch(
            tag,
            @"\b(?:name|id|autocomplete)\s*=\s*(['""])[^'""]*(?:access[_-]?token|api[_-]?key|code|mfa|otp|passcode|password|pin|pwd|secret|refresh[_-]?token|csrf|session|state|token|saml(?:request|response)|relaystate|wresult|wctx)[^'""]*\1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            return tag;
        }

        return Regex.Replace(
            tag,
            @"\bvalue\s*=\s*(['""])(.*?)\1",
            "value=$1<redacted>$1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
