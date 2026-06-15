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
        if (uri == null || (string.IsNullOrWhiteSpace(uri.Query) && string.IsNullOrWhiteSpace(uri.Fragment))) {
            return false;
        }

        return HasSensitiveQueryText(uri.Query) || HasSensitiveQueryText(uri.Fragment);
    }

    internal static bool HasSensitiveQueryText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        foreach (string parameters in GetParameterSegments(value)) {
            foreach (string pair in parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
                string name = pair.Split(new[] { '=' }, 2)[0];
                if (IsSensitiveName(Uri.UnescapeDataString(name))) {
                    return true;
                }
            }
        }

        return false;
    }

    internal static string RedactSensitiveQueryValues(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        int queryIndex = value.IndexOf('?');
        int fragmentIndex = value.IndexOf('#');
        if (queryIndex < 0 && fragmentIndex < 0) {
            return value;
        }

        if (queryIndex >= 0 && (fragmentIndex < 0 || queryIndex < fragmentIndex)) {
            int queryEnd = fragmentIndex >= 0 ? fragmentIndex : value.Length;
            string prefix = value.Substring(0, queryIndex + 1);
            string query = value.Substring(queryIndex + 1, queryEnd - queryIndex - 1);
            string fragment = fragmentIndex >= 0 ? "#" + RedactParameterPairs(value.Substring(fragmentIndex + 1)) : string.Empty;
            return prefix + RedactParameterPairs(query) + fragment;
        }

        return value.Substring(0, fragmentIndex + 1) + RedactParameterPairs(value.Substring(fragmentIndex + 1));
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
                return new[] { query, value.Substring(fragmentIndex + 1) };
            }

            return new[] { query };
        }

        return new[] { value.Substring(fragmentIndex + 1) };
    }

    private static string RedactParameterPairs(string parameters) {
        string[] pairs = parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        string[] redactedPairs = pairs.Select(pair => {
            string[] keyValue = pair.Split(new[] { '=' }, 2);
            string name = Uri.UnescapeDataString(keyValue[0]);
            return IsSensitiveName(name) ? keyValue[0] + "=<redacted>" : pair;
        }).ToArray();

        return string.Join("&", redactedPairs);
    }

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

        return redacted;
    }
}
