using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for turning observed browser network traffic into extraction candidates.
/// </summary>
public static partial class HtmlBrowser {
    private static readonly string[] AuthenticationHeaderNames = {
        "authorization",
        "cookie",
        "proxy-authorization",
        "x-csrf-token",
        "x-xsrf-token"
    };

    private static readonly string[] BrowserManagedHeaderNames = {
        ":authority",
        ":method",
        ":path",
        ":scheme",
        "accept-encoding",
        "accept-language",
        "cache-control",
        "connection",
        "content-length",
        "host",
        "origin",
        "pragma",
        "priority",
        "referer",
        "sec-fetch-dest",
        "sec-fetch-mode",
        "sec-fetch-site",
        "sec-fetch-user",
        "te",
        "upgrade-insecure-requests",
        "user-agent"
    };

    /// <summary>
    /// Finds browserless extraction candidates from the network traffic captured in a browser session.
    /// </summary>
    public static IReadOnlyList<HtmlBrowserlessDataSource> FindNetworkDataSources(
        HtmlBrowserSession session,
        HtmlBrowserNetworkDataSourceOptions? options = null) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        HtmlBrowserNetworkDataSourceOptions effectiveOptions = options ?? new HtmlBrowserNetworkDataSourceOptions();
        string pageUrl = !string.IsNullOrWhiteSpace(effectiveOptions.PageUrl)
            ? effectiveOptions.PageUrl!
            : session.Page.Url;
        return FindNetworkDataSources(session.NetworkLog, effectiveOptions, pageUrl);
    }

    /// <summary>
    /// Finds browserless extraction candidates from previously captured browser network entries.
    /// </summary>
    public static IReadOnlyList<HtmlBrowserlessDataSource> FindNetworkDataSources(
        IEnumerable<HtmlNetworkEntry> entries,
        HtmlBrowserNetworkDataSourceOptions? options = null,
        string? pageUrl = null) {
        if (entries == null) {
            throw new ArgumentNullException(nameof(entries));
        }

        HtmlBrowserNetworkDataSourceOptions effectiveOptions = options ?? new HtmlBrowserNetworkDataSourceOptions();
        string effectivePageUrl = FirstNonEmpty(effectiveOptions.PageUrl, pageUrl);
        HashSet<HtmlNetworkResourceType> resourceTypes = effectiveOptions.ResourceTypes.Count == 0
            ? new HashSet<HtmlNetworkResourceType> { HtmlNetworkResourceType.XHR, HtmlNetworkResourceType.Fetch }
            : new HashSet<HtmlNetworkResourceType>(effectiveOptions.ResourceTypes);

        IEnumerable<HtmlBrowserlessDataSource> query = entries
            .Where(entry => IsCandidate(entry, resourceTypes, effectiveOptions))
            .Select((entry, index) => CreateNetworkDataSource(entry, index, effectivePageUrl, effectiveOptions))
            .Where(source => effectiveOptions.IncludeExternal || !source.IsExternal)
            .OrderByDescending(source => source.CanExtractDirectly)
            .ThenBy(source => source.RiskLevel)
            .ThenBy(source => source.Index);

        if (effectiveOptions.MaxSources > 0) {
            query = query.Take(effectiveOptions.MaxSources);
        }

        return query.Select((source, index) => {
            source.Index = index;
            return source;
        }).ToArray();
    }

    private static bool IsCandidate(HtmlNetworkEntry entry, HashSet<HtmlNetworkResourceType> resourceTypes, HtmlBrowserNetworkDataSourceOptions options) {
        if (!resourceTypes.Contains(entry.ResourceType)) {
            return false;
        }

        if (!options.IncludeFailed && !IsSuccessful(entry)) {
            return false;
        }

        if (!options.IncludeNonGet && entry.Method != HtmlHttpMethod.Get) {
            return false;
        }

        return !string.IsNullOrWhiteSpace(entry.Url);
    }

    private static HtmlBrowserlessDataSource CreateNetworkDataSource(
        HtmlNetworkEntry entry,
        int index,
        string pageUrl,
        HtmlBrowserNetworkDataSourceOptions options) {
        Uri? endpointUri = TryCreateAbsoluteUri(entry.Url);
        Uri? pageUri = TryCreateAbsoluteUri(pageUrl);
        bool isExternal = endpointUri != null && pageUri != null && !HasSameOrigin(pageUri, endpointUri);
        bool isStateChanging = IsStateChanging(entry.Method);
        bool hasSensitiveUrl = endpointUri != null && HasSensitiveUrlAuthentication(endpointUri);
        IReadOnlyDictionary<string, string> observedRequestHeaders = BuildObservedRequestHeaders(entry.RequestHeaders);
        IReadOnlyDictionary<string, string> replayRequestHeaders = BuildReplayRequestHeaders(entry.RequestHeaders);
        IReadOnlyList<string> sensitiveRequestHeaderNames = BuildSensitiveRequestHeaderNames(entry.RequestHeaders);
        bool hasAuthenticationHint = HasAuthenticationHeader(entry.RequestHeaders) || hasSensitiveUrl || sensitiveRequestHeaderNames.Count > 0;
        HtmlApiEndpointRiskLevel riskLevel = ChooseNetworkRiskLevel(isExternal, isStateChanging, hasSensitiveUrl, hasAuthenticationHint);
        string responseBody = options.IncludeResponseBody ? entry.ResponseBody ?? string.Empty : string.Empty;
        bool hasResponseBody = !string.IsNullOrWhiteSpace(responseBody);
        bool canFetch = CanFetchObservedEndpoint(entry, riskLevel, isExternal, hasSensitiveUrl, hasAuthenticationHint);
        string redactedUrl = RedactNetworkUrlValues(entry.Url);

        return new HtmlBrowserlessDataSource {
            Index = index,
            Kind = "ObservedApiEndpoint",
            Name = BuildEndpointName(endpointUri, entry.Url),
            Type = entry.ResourceType.ToString(),
            PageUrl = RedactNetworkUrlValues(pageUrl),
            Url = redactedUrl,
            ResolvedUrl = redactedUrl,
            RedactedUrl = redactedUrl,
            Method = FormatMethod(entry.Method),
            RiskLevel = riskLevel,
            CanExtractDirectly = hasResponseBody || canFetch,
            RequiresHttpFetch = !hasResponseBody,
            IsExternal = isExternal,
            RequiresAuthenticationHint = hasAuthenticationHint,
            ObservedRequestHeaders = observedRequestHeaders,
            ReplayRequestHeaders = replayRequestHeaders,
            SensitiveRequestHeaderNames = sensitiveRequestHeaderNames,
            Source = "BrowserNetwork",
            RawContent = responseBody,
            SuggestedCommand = BuildSuggestedExtractionCommand(hasResponseBody, canFetch, riskLevel),
            SuggestedRecipeCommand = BuildSuggestedRecipeCommand(hasResponseBody),
            SuggestedReplayCommand = BuildSuggestedReplayCommand(replayRequestHeaders, sensitiveRequestHeaderNames),
            Evidence = BuildNetworkEvidence(entry, hasResponseBody, canFetch, riskLevel, observedRequestHeaders, replayRequestHeaders, sensitiveRequestHeaderNames),
            Warnings = BuildNetworkWarnings(entry, hasResponseBody, hasSensitiveUrl, hasAuthenticationHint, isExternal, isStateChanging, riskLevel, sensitiveRequestHeaderNames)
        };
    }

    private static string BuildSuggestedExtractionCommand(bool hasResponseBody, bool canFetch, HtmlApiEndpointRiskLevel riskLevel) {
        if (hasResponseBody) {
            return "$source | Invoke-HtmlDataExtraction";
        }

        if (canFetch) {
            return "$source | Invoke-HtmlDataExtraction -AllowHttpFetch";
        }

        return "$source | Format-List RedactedUrl,Method,RiskLevel,Warnings";
    }

    private static string BuildSuggestedRecipeCommand(bool hasResponseBody) =>
        hasResponseBody
            ? "$source | Export-HtmlExtractionRecipe -Path '.\\observed-api.recipe.json' -IncludeRawContent"
            : "$source | Export-HtmlExtractionRecipe -Path '.\\observed-api.recipe.json'";

    private static string BuildSuggestedReplayCommand(
        IReadOnlyDictionary<string, string> replayRequestHeaders,
        IReadOnlyList<string> sensitiveRequestHeaderNames) {
        if (sensitiveRequestHeaderNames.Count > 0) {
            return "$webSession = ConvertTo-HtmlWebRequestSession -Session $session; Invoke-WebRequest -Uri $source.ResolvedUrl -WebSession $webSession -Headers $source.ReplayRequestHeaders";
        }

        if (replayRequestHeaders.Count > 0) {
            return "Invoke-WebRequest -Uri $source.ResolvedUrl -Headers $source.ReplayRequestHeaders";
        }

        return "Invoke-WebRequest -Uri $source.ResolvedUrl";
    }

    private static bool CanFetchObservedEndpoint(HtmlNetworkEntry entry, HtmlApiEndpointRiskLevel riskLevel, bool isExternal, bool hasSensitiveUrl, bool hasAuthenticationHint) =>
        entry.Method == HtmlHttpMethod.Get
        && IsSuccessful(entry)
        && riskLevel == HtmlApiEndpointRiskLevel.Low
        && !isExternal
        && !hasSensitiveUrl
        && !hasAuthenticationHint;

    private static IReadOnlyList<string> BuildNetworkEvidence(
        HtmlNetworkEntry entry,
        bool hasResponseBody,
        bool canFetch,
        HtmlApiEndpointRiskLevel riskLevel,
        IReadOnlyDictionary<string, string> observedRequestHeaders,
        IReadOnlyDictionary<string, string> replayRequestHeaders,
        IReadOnlyList<string> sensitiveRequestHeaderNames) {
        List<string> evidence = new() {
            $"Observed {FormatMethod(entry.Method)} {entry.ResourceType} request in browser network traffic.",
            $"Risk classification: {riskLevel}."
        };

        if (entry.Status.HasValue) {
            evidence.Add($"Observed response status: {(int)entry.Status.Value}.");
        }

        if (hasResponseBody) {
            evidence.Add("Captured response body is available for browserless extraction without replaying the request.");
        } else if (canFetch) {
            evidence.Add("Endpoint is a same-origin low-risk GET candidate and can be fetched when HTTP extraction is allowed.");
        } else {
            evidence.Add("Endpoint needs operator review before direct HTTP extraction.");
        }

        if (observedRequestHeaders.Count > 0) {
            evidence.Add($"Captured {observedRequestHeaders.Count} request header hint(s) for replay review.");
        }

        if (replayRequestHeaders.Count > 0) {
            evidence.Add($"Prepared {replayRequestHeaders.Count} non-sensitive request header(s) for browserless replay.");
        }

        if (sensitiveRequestHeaderNames.Count > 0) {
            evidence.Add($"Redacted sensitive request header value(s): {string.Join(", ", sensitiveRequestHeaderNames)}.");
        }

        return evidence;
    }

    private static IReadOnlyList<string> BuildNetworkWarnings(
        HtmlNetworkEntry entry,
        bool hasResponseBody,
        bool hasSensitiveUrl,
        bool hasAuthenticationHint,
        bool isExternal,
        bool isStateChanging,
        HtmlApiEndpointRiskLevel riskLevel,
        IReadOnlyList<string> sensitiveRequestHeaderNames) {
        List<string> warnings = new();
        if (!IsSuccessful(entry)) {
            warnings.Add("Observed request did not complete with a successful response.");
        }

        if (isStateChanging) {
            warnings.Add($"Observed method is {FormatMethod(entry.Method)}; browserless extraction only auto-fetches low-risk GET candidates.");
        }

        if (isExternal) {
            warnings.Add("Observed endpoint is external to the page origin.");
        }

        if (hasAuthenticationHint) {
            warnings.Add("Observed request contains authentication or token hints.");
        }

        if (sensitiveRequestHeaderNames.Count > 0) {
            warnings.Add("ObservedRequestHeaders redacts sensitive header values; do not copy Authorization, Cookie, CSRF, or token values from browser tools into scripts.");
        }

        if (sensitiveRequestHeaderNames.Any(name => string.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase))) {
            warnings.Add("Authorization header replay usually needs a supported API token flow; browser cookies alone may not be enough for this endpoint.");
        }

        if (sensitiveRequestHeaderNames.Any(name => string.Equals(name, "cookie", StringComparison.OrdinalIgnoreCase))) {
            warnings.Add("Cookie-backed replay should use ConvertTo-HtmlWebRequestSession instead of copying Cookie header text.");
        }

        if (hasSensitiveUrl) {
            warnings.Add("Observed endpoint contains sensitive query or fragment parameter names or URL user-info credentials.");
        }

        if (entry.ResponseBodyTruncated) {
            warnings.Add("Captured response body was truncated.");
        }

        if (entry.ResponseBodyRedacted) {
            warnings.Add("Captured response body was redacted before data-source discovery.");
        }

        if (!string.IsNullOrWhiteSpace(entry.ResponseBodyError)) {
            warnings.Add($"Response body capture reported: {entry.ResponseBodyError}");
        }

        if (!hasResponseBody && riskLevel != HtmlApiEndpointRiskLevel.Low) {
            warnings.Add($"Endpoint risk is {riskLevel}; captured response body or explicit operator review is recommended.");
        }

        return warnings;
    }

    private static HtmlApiEndpointRiskLevel ChooseNetworkRiskLevel(bool isExternal, bool isStateChanging, bool hasSensitiveUrl, bool hasAuthenticationHint) {
        if (isStateChanging) {
            return HtmlApiEndpointRiskLevel.High;
        }

        return isExternal || hasSensitiveUrl || hasAuthenticationHint
            ? HtmlApiEndpointRiskLevel.Medium
            : HtmlApiEndpointRiskLevel.Low;
    }

    private static bool IsSuccessful(HtmlNetworkEntry entry) =>
        entry.Status.HasValue && (int)entry.Status.Value >= 200 && (int)entry.Status.Value < 400 && string.IsNullOrWhiteSpace(entry.FailureText);

    private static bool IsStateChanging(HtmlHttpMethod method) =>
        method != HtmlHttpMethod.Get && method != HtmlHttpMethod.Head && method != HtmlHttpMethod.Options;

    private static bool HasAuthenticationHeader(IDictionary<string, string>? headers) =>
        headers != null && headers.Keys.Any(key => AuthenticationHeaderNames.Contains(key, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, string> BuildObservedRequestHeaders(IDictionary<string, string>? headers) {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (headers == null) {
            return result;
        }

        foreach (KeyValuePair<string, string> header in headers.OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)) {
            if (!ShouldIncludeReplayHeaderHint(header.Key)) {
                continue;
            }

            result[header.Key] = IsSensitiveHeaderName(header.Key)
                ? "<redacted>"
                : HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(header.Value ?? string.Empty);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildReplayRequestHeaders(IDictionary<string, string>? headers) {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (headers == null) {
            return result;
        }

        foreach (KeyValuePair<string, string> header in headers.OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)) {
            if (!ShouldIncludeReplayHeaderHint(header.Key) || IsSensitiveHeaderName(header.Key)) {
                continue;
            }

            string value = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(header.Value ?? string.Empty);
            if (!string.Equals(value, header.Value ?? string.Empty, StringComparison.Ordinal)) {
                continue;
            }

            result[header.Key] = value;
        }

        return result;
    }

    private static IReadOnlyList<string> BuildSensitiveRequestHeaderNames(IDictionary<string, string>? headers) {
        if (headers == null) {
            return Array.Empty<string>();
        }

        return headers.Keys
            .Where(ShouldIncludeReplayHeaderHint)
            .Where(IsSensitiveHeaderName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ShouldIncludeReplayHeaderHint(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return false;
        }

        string normalized = name.Trim();
        if (BrowserManagedHeaderNames.Contains(normalized, StringComparer.OrdinalIgnoreCase)) {
            return false;
        }

        if (normalized.StartsWith("sec-", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return IsSensitiveHeaderName(normalized)
            || normalized.Equals("accept", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("content-type", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("if-match", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("if-none-match", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("prefer", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("traceparent", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("tracestate", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("x-", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(normalized, "(api|auth|csrf|xsrf|tenant|token|version|correlation|request-id)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsSensitiveHeaderName(string name) =>
        AuthenticationHeaderNames.Contains(name, StringComparer.OrdinalIgnoreCase)
        || HtmlSensitiveValueRedactor.IsSensitiveName(name);

    private static bool HasSensitiveUrlAuthentication(Uri uri) =>
        !string.IsNullOrWhiteSpace(uri.UserInfo)
        || HtmlSensitiveValueRedactor.HasSensitiveQueryText(uri.Query)
        || HtmlSensitiveValueRedactor.HasSensitiveQueryText(uri.Fragment);

    private static string RedactNetworkUrlValues(string value) {
        string redactedValue = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(value);
        int queryIndex = redactedValue.IndexOf('?');
        int fragmentIndex = redactedValue.IndexOf('#');
        if (queryIndex < 0 && fragmentIndex < 0) {
            return redactedValue;
        }

        if (queryIndex >= 0 && (fragmentIndex < 0 || queryIndex < fragmentIndex)) {
            int queryEnd = fragmentIndex >= 0 ? fragmentIndex : redactedValue.Length;
            string prefix = redactedValue.Substring(0, queryIndex + 1);
            string query = redactedValue.Substring(queryIndex + 1, queryEnd - queryIndex - 1);
            string fragment = fragmentIndex >= 0 ? "#" + RedactNetworkParameterPairs(redactedValue.Substring(fragmentIndex + 1)) : string.Empty;
            return prefix + RedactNetworkParameterPairs(query) + fragment;
        }

        return redactedValue.Substring(0, fragmentIndex + 1) + RedactNetworkParameterPairs(redactedValue.Substring(fragmentIndex + 1));
    }

    private static string RedactNetworkParameterPairs(string parameters) {
        string[] pairs = parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("&", pairs.Select(static pair => {
            string[] keyValue = pair.Split(new[] { '=' }, 2);
            string name = Uri.UnescapeDataString(keyValue[0]);
            return HtmlSensitiveValueRedactor.IsSensitiveName(name)
                ? keyValue[0] + "=<redacted>"
                : pair;
        }));
    }

    private static Uri? TryCreateAbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;

    private static bool HasSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    private static string BuildEndpointName(Uri? uri, string fallback) {
        if (uri == null) {
            return fallback;
        }

        string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(lastSegment) ? uri.Host : lastSegment;
    }

    private static string FormatMethod(HtmlHttpMethod method) =>
        method.ToString().ToUpperInvariant();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
