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
        if (uri == null || string.IsNullOrWhiteSpace(uri.Query)) {
            return false;
        }

        return HasSensitiveQueryText(uri.Query);
    }

    internal static bool HasSensitiveQueryText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        int queryIndex = value.IndexOf('?');
        string query = queryIndex >= 0 ? value.Substring(queryIndex + 1) : value.TrimStart('?');
        int fragmentIndex = query.IndexOf('#');
        if (fragmentIndex >= 0) {
            query = query.Substring(0, fragmentIndex);
        }

        foreach (string pair in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
            string name = pair.Split(new[] { '=' }, 2)[0];
            if (IsSensitiveName(Uri.UnescapeDataString(name))) {
                return true;
            }
        }

        return false;
    }

    internal static string RedactSensitiveQueryValues(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        int queryIndex = value.IndexOf('?');
        if (queryIndex < 0) {
            return value;
        }

        int fragmentIndex = value.IndexOf('#', queryIndex);
        string prefix = value.Substring(0, queryIndex + 1);
        string query = fragmentIndex >= 0
            ? value.Substring(queryIndex + 1, fragmentIndex - queryIndex - 1)
            : value.Substring(queryIndex + 1);
        string fragment = fragmentIndex >= 0 ? value.Substring(fragmentIndex) : string.Empty;
        string[] pairs = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        string[] redactedPairs = pairs.Select(pair => {
            string[] keyValue = pair.Split(new[] { '=' }, 2);
            string name = Uri.UnescapeDataString(keyValue[0]);
            return IsSensitiveName(name) ? keyValue[0] + "=<redacted>" : pair;
        }).ToArray();

        return prefix + string.Join("&", redactedPairs) + fragment;
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
