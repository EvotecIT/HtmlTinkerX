using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Builds classified endpoint inventories from page workbench interaction surfaces.
/// </summary>
public static class HtmlApiEndpointInventory {
    private static readonly HashSet<string> StateChangingMethods = new(StringComparer.OrdinalIgnoreCase) {
        "POST",
        "PUT",
        "PATCH",
        "DELETE"
    };

    private static readonly string[] AuthHints = {
        "auth",
        "login",
        "oauth",
        "saml",
        "signin",
        "sso",
        "token"
    };

    private static readonly string[] SensitiveQueryNames = {
        "access_token",
        "api_key",
        "apikey",
        "auth",
        "code",
        "credential",
        "key",
        "password",
        "refresh_token",
        "secret",
        "session",
        "token"
    };

    /// <summary>
    /// Builds a classified endpoint inventory from a page workbench result.
    /// </summary>
    /// <param name="workbench">Page workbench result.</param>
    /// <param name="options">Inventory options.</param>
    /// <returns>Deduplicated endpoint records.</returns>
    public static IReadOnlyList<HtmlApiEndpointRecord> Build(HtmlPageWorkbenchResult workbench, HtmlApiEndpointInventoryOptions? options = null) {
        if (workbench == null) {
            throw new ArgumentNullException(nameof(workbench));
        }

        HtmlApiEndpointInventoryOptions effectiveOptions = options ?? new HtmlApiEndpointInventoryOptions();
        Uri? pageUri = TryCreateUri(FirstNonEmpty(workbench.FinalUrl, workbench.SourceUrl));
        List<HtmlApiEndpointRecord> records = new();
        foreach (HtmlInteractionSurfaceItem item in workbench.InteractionSurface) {
            bool isForm = item.Kind.Equals("Form", StringComparison.OrdinalIgnoreCase);
            bool isScriptEndpoint = item.Kind.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)
                || item.Kind.Equals("LinkedEndpoint", StringComparison.OrdinalIgnoreCase);
            if ((isForm && !effectiveOptions.IncludeForms) || (isScriptEndpoint && !effectiveOptions.IncludeScriptEndpoints) || (!isForm && !isScriptEndpoint)) {
                continue;
            }

            records.Add(CreateRecord(item, pageUri, workbench));
        }

        return records
            .GroupBy(static record => string.Join("|", record.Kind, record.Method, record.ResolvedUrl, record.Url), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static record => record.RiskLevel)
            .ThenBy(static record => record.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static record => record.ResolvedUrl, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HtmlApiEndpointRecord CreateRecord(HtmlInteractionSurfaceItem item, Uri? pageUri, HtmlPageWorkbenchResult workbench) {
        Uri? resolvedUri = ResolveUri(item.Url, pageUri);
        string method = NormalizeMethod(item.Method, item.Kind);
        bool isExternal = resolvedUri != null && pageUri != null && !HasSameOrigin(pageUri, resolvedUri);
        bool isStateChanging = StateChangingMethods.Contains(method);
        bool hasSensitiveQuery = HasSensitiveQuery(resolvedUri);
        bool hasAuthHint = HasAuthHint(item, resolvedUri, workbench);
        List<string> reasonCodes = BuildReasonCodes(item, isExternal, isStateChanging, hasSensitiveQuery, hasAuthHint, resolvedUri);
        HtmlApiEndpointRiskLevel riskLevel = ChooseRiskLevel(isExternal, isStateChanging, hasSensitiveQuery, hasAuthHint);

        return new HtmlApiEndpointRecord {
            Kind = item.Kind,
            Name = FirstNonEmpty(item.Name, resolvedUri?.AbsolutePath, item.Url),
            Method = method,
            Url = item.Url,
            ResolvedUrl = resolvedUri?.AbsoluteUri ?? item.Url,
            Origin = GetOrigin(resolvedUri),
            IsExternal = isExternal,
            IsStateChanging = isStateChanging,
            RequiresAuthenticationHint = hasAuthHint,
            HasSensitiveQuery = hasSensitiveQuery,
            RiskLevel = riskLevel,
            ReasonCodes = reasonCodes,
            Selector = item.Selector,
            Source = item.Source,
            Metadata = item.Metadata
        };
    }

    private static List<string> BuildReasonCodes(HtmlInteractionSurfaceItem item, bool isExternal, bool isStateChanging, bool hasSensitiveQuery, bool hasAuthHint, Uri? resolvedUri) {
        List<string> reasonCodes = new();
        if (item.Kind.Equals("Form", StringComparison.OrdinalIgnoreCase)) {
            reasonCodes.Add("form-action");
        }

        if (item.Kind.Equals("LinkedEndpoint", StringComparison.OrdinalIgnoreCase)) {
            reasonCodes.Add("linked-script");
        }

        if (isExternal) {
            reasonCodes.Add("external-origin");
        }

        if (isStateChanging) {
            reasonCodes.Add("state-changing-method");
        }

        if (hasSensitiveQuery) {
            reasonCodes.Add("sensitive-query-name");
        }

        if (hasAuthHint) {
            reasonCodes.Add("auth-hint");
        }

        if (resolvedUri == null) {
            reasonCodes.Add("unresolved-url");
        }

        if (reasonCodes.Count == 0) {
            reasonCodes.Add("same-origin-read");
        }

        return reasonCodes;
    }

    private static HtmlApiEndpointRiskLevel ChooseRiskLevel(bool isExternal, bool isStateChanging, bool hasSensitiveQuery, bool hasAuthHint) {
        if (isStateChanging || hasSensitiveQuery) {
            return HtmlApiEndpointRiskLevel.High;
        }

        return isExternal || hasAuthHint
            ? HtmlApiEndpointRiskLevel.Medium
            : HtmlApiEndpointRiskLevel.Low;
    }

    private static string NormalizeMethod(string method, string kind) {
        if (!string.IsNullOrWhiteSpace(method)) {
            return method.ToUpperInvariant();
        }

        return kind.Equals("Form", StringComparison.OrdinalIgnoreCase) ? "GET" : "UNKNOWN";
    }

    private static Uri? ResolveUri(string url, Uri? pageUri) {
        if (string.IsNullOrWhiteSpace(url)) {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absoluteUri)) {
            return absoluteUri;
        }

        return pageUri != null && Uri.TryCreate(pageUri, url, out Uri? resolvedUri)
            ? resolvedUri
            : null;
    }

    private static Uri? TryCreateUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri : null;

    private static bool HasSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    private static bool HasSensitiveQuery(Uri? uri) {
        if (uri == null || string.IsNullOrWhiteSpace(uri.Query)) {
            return false;
        }

        foreach (string pair in uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
            string name = pair.Split(new[] { '=' }, 2)[0];
            if (SensitiveQueryNames.Any(sensitive => string.Equals(Uri.UnescapeDataString(name), sensitive, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
        }

        return false;
    }

    private static bool HasAuthHint(HtmlInteractionSurfaceItem item, Uri? uri, HtmlPageWorkbenchResult workbench) {
        if (workbench.ExtractionPlan?.HasLoginForm == true || workbench.ExtractionPlan?.HasAutoSubmitForm == true) {
            return true;
        }

        string combined = string.Join(" ", item.Name, item.Url, item.Metadata, uri?.AbsolutePath ?? string.Empty);
        return AuthHints.Any(hint => combined.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string GetOrigin(Uri? uri) =>
        uri == null ? string.Empty : uri.GetLeftPart(UriPartial.Authority);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
