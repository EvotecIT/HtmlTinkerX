using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>Normalizes page addresses so the same page found through different links is recognised as one.</summary>
public static class HtmlUrlNormalizer {
    /// <summary>
    /// Query-parameter name patterns removed as tracking parameters; <c>*</c> matches any characters. The crawler's
    /// <see cref="HtmlCrawlOptions.IgnoredQueryParameterPatterns"/> starts from the same list.
    /// </summary>
    public static IReadOnlyList<string> TrackingParameterPatterns { get; } = new[] {
        "utm_*", "fbclid", "gclid", "msclkid", "mc_*", "_hs*", "vero_*", "yclid"
    };

    /// <summary>
    /// Returns a canonical form of an absolute address: lower-case scheme and host, no default port, no fragment,
    /// query parameters in ordinal order without tracking parameters, and no trailing slash when there is no query.
    /// </summary>
    /// <param name="uri">An absolute address.</param>
    /// <param name="removeTrackingParameters">Whether parameters matching <see cref="TrackingParameterPatterns"/> are removed.</param>
    public static Uri Normalize(Uri uri, bool removeTrackingParameters = true) {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        if (!uri.IsAbsoluteUri) throw new ArgumentException("URL must be absolute.", nameof(uri));

        UriBuilder builder = new(uri) {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };
        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) || (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443)) {
            builder.Port = -1;
        }

        string query = builder.Query.TrimStart('?');
        if (query.Length > 0) {
            if (removeTrackingParameters) query = RemoveQueryParameters(query, TrackingParameterPatterns);
            builder.Query = string.Join("&", query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => part.Trim()).OrderBy(static part => part, StringComparer.Ordinal));
        }

        string normalized = builder.Uri.AbsoluteUri;
        return normalized.EndsWith("/", StringComparison.Ordinal) && builder.Query.Length == 0
            ? new Uri(normalized.TrimEnd('/'))
            : new Uri(normalized);
    }

    /// <summary>Removes query parameters whose decoded names match any pattern, keeping the others in order.</summary>
    internal static string RemoveQueryParameters(string query, IEnumerable<string> patterns) {
        string rawQuery = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        if (string.IsNullOrWhiteSpace(rawQuery)) {
            return string.Empty;
        }

        List<string> kept = new();
        foreach (string part in rawQuery.Split('&')) {
            if (string.IsNullOrWhiteSpace(part)) {
                continue;
            }

            int separatorIndex = part.IndexOf('=');
            string rawName = separatorIndex >= 0 ? part.Substring(0, separatorIndex) : part;
            string parameterName = Uri.UnescapeDataString(rawName.Replace("+", "%20"));
            if (MatchesPattern(parameterName, patterns)) {
                continue;
            }

            kept.Add(part);
        }

        return string.Join("&", kept);
    }

    internal static bool MatchesPattern(string parameterName, IEnumerable<string> patterns) {
        foreach (string pattern in patterns) {
            if (string.IsNullOrWhiteSpace(pattern)) {
                continue;
            }

            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(parameterName, regexPattern, RegexOptions.IgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
