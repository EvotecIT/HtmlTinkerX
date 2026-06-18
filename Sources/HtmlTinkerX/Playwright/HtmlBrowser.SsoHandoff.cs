using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helpers for observing enterprise SSO handoff forms after user-attended authentication.
/// </summary>
public static partial class HtmlBrowser {
    private static readonly string[] SsoHandoffFieldNames = {
        "samlresponse",
        "samlrequest",
        "relaystate",
        "wa",
        "wresult",
        "wctx",
        "code",
        "id_token",
        "access_token",
        "refresh_token",
        "state",
        "error",
        "error_description",
        "error_uri",
        "session_state"
    };

    /// <summary>
    /// JavaScript installed before page scripts when SSO handoff forms should be held for inspection.
    /// </summary>
    public const string PreventSsoAutoSubmitInitScript = @"(() => {
        const names = new Set(['samlresponse', 'samlrequest', 'relaystate', 'wa', 'wresult', 'wctx', 'code', 'id_token', 'access_token', 'refresh_token', 'state']);
        const hasSsoField = form => {
            if (!form || !form.elements) return false;
            return Array.from(form.elements).some(element => element && element.name && names.has(String(element.name).toLowerCase()));
        };
        const mark = form => {
            try {
                form.setAttribute('data-htmltinkerx-sso-handoff', 'true');
                window.__HtmlTinkerXSsoHandoffCaptured = true;
            } catch (_) {
            }
        };
        const originalSubmit = HTMLFormElement.prototype.submit;
        HTMLFormElement.prototype.submit = function() {
            if (hasSsoField(this)) {
                mark(this);
                return undefined;
            }

            return originalSubmit.apply(this, arguments);
        };
        if (HTMLFormElement.prototype.requestSubmit) {
            const originalRequestSubmit = HTMLFormElement.prototype.requestSubmit;
            HTMLFormElement.prototype.requestSubmit = function() {
                if (hasSsoField(this)) {
                    mark(this);
                    return undefined;
                }

                return originalRequestSubmit.apply(this, arguments);
            };
        }
        document.addEventListener('submit', event => {
            if (hasSsoField(event.target)) {
                mark(event.target);
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, true);
    })();";

    /// <summary>
    /// Finds SAML, WS-Federation, OAuth, or OpenID Connect handoff forms in the current browser page.
    /// </summary>
    /// <param name="session">Browser session to inspect.</param>
    /// <param name="options">Inspection and sensitive-value handling options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Observed SSO handoff forms.</returns>
    public static async Task<IReadOnlyList<HtmlBrowserSsoHandoff>> GetSsoHandoffsAsync(
        HtmlBrowserSession session,
        HtmlBrowserSsoHandoffOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        options ??= new HtmlBrowserSsoHandoffOptions();
        if (options.MaxValueLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxValueLength cannot be negative.");
        }
        if (options.Timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout cannot be negative.");
        }
        if (options.PollMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options), "PollMilliseconds must be greater than zero.");
        }

        if (options.Wait) {
            return await WaitForSsoHandoffsAsync(session, options, cancellationToken).ConfigureAwait(false);
        }

        return await ReadSsoHandoffsAsync(session, options, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<HtmlBrowserSsoHandoff>> WaitForSsoHandoffsAsync(
        HtmlBrowserSession session,
        HtmlBrowserSsoHandoffOptions options,
        CancellationToken cancellationToken) {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        while (true) {
            IReadOnlyList<HtmlBrowserSsoHandoff> handoffs = await ReadSsoHandoffsAsync(session, options, cancellationToken).ConfigureAwait(false);
            if (handoffs.Count > 0) {
                return handoffs;
            }

            if (options.Timeout > 0 && DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(options.Timeout)) {
                string context = await GetSsoHandoffWaitContextAsync(session, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException($"Timed out after {options.Timeout} ms waiting for an SSO handoff form.{context}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await session.Page.WaitForTimeoutAsync(options.PollMilliseconds).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> GetSsoHandoffWaitContextAsync(HtmlBrowserSession session, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string pageUrl = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(session.Page.Url ?? string.Empty);
        string title = string.Empty;
        try {
            title = await session.Page.TitleAsync().ConfigureAwait(false);
        } catch (Exception ex) when (!(ex is OperationCanceledException)) {
            title = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(pageUrl)) {
            return $" Current page: '{title}' ({pageUrl}).";
        }

        if (!string.IsNullOrWhiteSpace(pageUrl)) {
            return $" Current page: {pageUrl}.";
        }

        return string.Empty;
    }

    private static async Task<IReadOnlyList<HtmlBrowserSsoHandoff>> ReadSsoHandoffsAsync(
        HtmlBrowserSession session,
        HtmlBrowserSsoHandoffOptions options,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string json = await session.Page.EvaluateAsync<string>(SsoHandoffScript).ConfigureAwait(false);
        List<HtmlBrowserSsoHandoff> handoffs = new();

        using JsonDocument document = JsonDocument.Parse(json);
        string pageUrlRaw = GetJsonString(document.RootElement, "url") ?? session.Page.Url;
        string pageUrl = RedactSsoUrlValues(pageUrlRaw);
        string title = GetJsonString(document.RootElement, "title") ?? string.Empty;
        HtmlBrowserSsoHandoff? urlHandoff = BuildUrlSsoHandoff(pageUrlRaw, pageUrl, title, options);
        if (urlHandoff != null) {
            handoffs.Add(urlHandoff);
        }

        if (document.RootElement.TryGetProperty("forms", out JsonElement forms) && forms.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement form in forms.EnumerateArray()) {
                List<HtmlBrowserSsoField> fields = ReadSsoFields(form, options);
                HtmlBrowserSsoHandoffKind kind = ClassifySsoHandoff(fields);
                if (!options.IncludeAllForms && kind == HtmlBrowserSsoHandoffKind.Unknown) {
                    continue;
                }

                handoffs.Add(new HtmlBrowserSsoHandoff {
                    Index = GetJsonInt(form, "index"),
                    Kind = kind,
                    PageUrl = pageUrl,
                    Title = title,
                    FormSelector = GetJsonString(form, "selector") ?? string.Empty,
                    Action = GetJsonString(form, "action") ?? string.Empty,
                    Method = GetJsonString(form, "method") ?? string.Empty,
                    AutoSubmitPrevented = GetJsonBool(form, "autoSubmitPrevented"),
                    ContainsSensitiveValues = fields.Any(static field => field.IsSensitive),
                    Fields = fields,
                    FormData = BuildSsoFormData(fields),
                    SuggestedCommand = BuildSsoSuggestedCommand(),
                    Warnings = BuildSsoWarnings(fields, kind)
                });
            }
        }

        return handoffs;
    }

    private static HtmlBrowserSsoHandoff? BuildUrlSsoHandoff(string pageUrlRaw, string pageUrl, string title, HtmlBrowserSsoHandoffOptions options) {
        if (string.IsNullOrWhiteSpace(pageUrlRaw) || !Uri.TryCreate(pageUrlRaw, UriKind.Absolute, out Uri? uri)) {
            return null;
        }

        List<HtmlBrowserSsoField> fields = ReadUrlSsoFields(uri, options);
        HtmlBrowserSsoHandoffKind kind = ClassifySsoHandoff(fields);
        if (kind == HtmlBrowserSsoHandoffKind.Unknown && !fields.Any(static field => string.Equals(field.Name, "error", StringComparison.OrdinalIgnoreCase))) {
            return null;
        }

        string action = uri.GetLeftPart(UriPartial.Path);
        return new HtmlBrowserSsoHandoff {
            Index = -1,
            Kind = kind,
            PageUrl = pageUrl,
            Title = title,
            FormSelector = "location",
            Action = action,
            Method = "GET",
            AutoSubmitPrevented = false,
            ContainsSensitiveValues = fields.Any(static field => field.IsSensitive),
            Fields = fields,
            FormData = BuildSsoFormData(fields),
            SuggestedCommand = "$analysis = Get-HtmlBrowserSsoHandoff -Session $session -Analyze",
            Warnings = BuildSsoWarnings(fields, kind)
        };
    }

    private static List<HtmlBrowserSsoField> ReadUrlSsoFields(Uri uri, HtmlBrowserSsoHandoffOptions options) {
        List<HtmlBrowserSsoField> fields = new();
        AddUrlFields(fields, uri.Query, "url-query", options);
        AddFragmentUrlFields(fields, uri.Fragment, options);
        return fields
            .Where(static field => SsoHandoffFieldNames.Contains(field.Name.ToLowerInvariant()))
            .ToList();
    }

    private static void AddFragmentUrlFields(List<HtmlBrowserSsoField> fields, string fragment, HtmlBrowserSsoHandoffOptions options) {
        if (string.IsNullOrWhiteSpace(fragment)) {
            return;
        }

        string trimmed = fragment.TrimStart('#');
        int queryIndex = trimmed.IndexOf('?');
        string parameters = queryIndex >= 0
            ? trimmed.Substring(queryIndex + 1)
            : trimmed;
        AddUrlFields(fields, parameters, "url-fragment", options);
    }

    private static void AddUrlFields(List<HtmlBrowserSsoField> fields, string component, string type, HtmlBrowserSsoHandoffOptions options) {
        if (string.IsNullOrWhiteSpace(component)) {
            return;
        }

        string trimmed = component.TrimStart('?', '#');
        foreach (string pair in trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
            string[] parts = pair.Split(new[] { '=' }, 2);
            string name = WebUtility.UrlDecode(parts[0]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) {
                continue;
            }

            string rawValue = parts.Length > 1
                ? WebUtility.UrlDecode(parts[1]) ?? string.Empty
                : string.Empty;
            fields.Add(BuildSsoField(name, type, rawValue, options));
        }
    }

    private static Dictionary<string, string> BuildSsoFormData(IEnumerable<HtmlBrowserSsoField> fields) {
        Dictionary<string, string> formData = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlBrowserSsoField field in fields) {
            if (string.IsNullOrWhiteSpace(field.Name)) {
                continue;
            }

            formData[field.Name] = field.Value;
        }

        return formData;
    }

    private static string RedactSsoUrlValues(string value) {
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
            string fragment = fragmentIndex >= 0 ? "#" + RedactSsoFragmentParameterPairs(redactedValue.Substring(fragmentIndex + 1)) : string.Empty;
            return prefix + RedactSsoParameterPairs(query) + fragment;
        }

        return redactedValue.Substring(0, fragmentIndex + 1) + RedactSsoFragmentParameterPairs(redactedValue.Substring(fragmentIndex + 1));
    }

    private static string RedactSsoFragmentParameterPairs(string fragment) {
        int queryIndex = fragment.IndexOf('?');
        if (queryIndex < 0) {
            return RedactSsoParameterPairs(fragment);
        }

        return fragment.Substring(0, queryIndex + 1) + RedactSsoParameterPairs(fragment.Substring(queryIndex + 1));
    }

    private static string RedactSsoParameterPairs(string parameters) {
        string[] pairs = parameters.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("&", pairs.Select(static pair => {
            string[] keyValue = pair.Split(new[] { '=' }, 2);
            string name = WebUtility.UrlDecode(keyValue[0]) ?? string.Empty;
            return SsoHandoffFieldNames.Contains(name.ToLowerInvariant())
                ? keyValue[0] + "=<redacted>"
                : pair;
        }));
    }

    private static string BuildSsoSuggestedCommand() =>
        "$webSession = ConvertTo-HtmlWebRequestSession -Session $session; Invoke-WebRequest -Uri $handoff.Action -Method $handoff.Method -Body $handoff.FormData -WebSession $webSession";

    private static List<string> BuildSsoWarnings(IReadOnlyCollection<HtmlBrowserSsoField> fields, HtmlBrowserSsoHandoffKind kind) {
        List<string> warnings = new();
        if (fields.Any(static field => field.Redacted)) {
            warnings.Add("FormData contains redacted values. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues only when you intentionally need to replay the handoff.");
        }

        if (fields.Any(static field => field.Truncated)) {
            warnings.Add("One or more field values were truncated. Increase -MaxValueLength or set it to 0 before replaying the handoff.");
        }

        string[] duplicateNames = fields
            .Where(static field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateNames.Length > 0) {
            warnings.Add($"Duplicate form field names were observed ({string.Join(", ", duplicateNames)}). FormData keeps the last value for each name; use Fields if duplicate values matter.");
        }

        if (kind == HtmlBrowserSsoHandoffKind.Saml || kind == HtmlBrowserSsoHandoffKind.WsFederation) {
            warnings.Add("SSO assertions are often short-lived or single-use. Capture immediately before replay and avoid storing revealed values in logs.");
        }

        if (fields.Any(static field => field.Type.StartsWith("url-", StringComparison.OrdinalIgnoreCase))) {
            warnings.Add("SSO values were found in the current URL. Browser history, proxy logs, and transcripts may retain query or fragment values; prefer safe analysis output over logging the raw URL.");
        }

        return warnings;
    }

    private static List<HtmlBrowserSsoField> ReadSsoFields(JsonElement form, HtmlBrowserSsoHandoffOptions options) {
        List<HtmlBrowserSsoField> fields = new();
        if (!form.TryGetProperty("fields", out JsonElement rawFields) || rawFields.ValueKind != JsonValueKind.Array) {
            return fields;
        }

        foreach (JsonElement field in rawFields.EnumerateArray()) {
            fields.Add(BuildSsoField(
                GetJsonString(field, "name") ?? string.Empty,
                GetJsonString(field, "type") ?? string.Empty,
                GetJsonString(field, "value") ?? string.Empty,
                options));
        }

        return fields;
    }

    private static HtmlBrowserSsoField BuildSsoField(string name, string type, string rawValue, HtmlBrowserSsoHandoffOptions options) {
        bool isSensitive = IsSensitiveSsoField(name);
        bool redacted = isSensitive && !options.IncludeSensitiveValues;
        bool truncated = false;
        string value = redacted ? "<redacted>" : rawValue;
        if (!redacted && options.MaxValueLength > 0 && value.Length > options.MaxValueLength) {
            value = value.Substring(0, options.MaxValueLength);
            truncated = true;
        }

        return new HtmlBrowserSsoField {
            Name = name,
            Type = type,
            Value = value,
            ValueLength = rawValue.Length,
            IsSensitive = isSensitive,
            Redacted = redacted,
            Truncated = truncated
        };
    }

    private static HtmlBrowserSsoHandoffKind ClassifySsoHandoff(IReadOnlyCollection<HtmlBrowserSsoField> fields) {
        HashSet<string> names = new(fields.Select(static field => field.Name), StringComparer.OrdinalIgnoreCase);
        if (names.Contains("samlresponse") || names.Contains("samlrequest") || names.Contains("relaystate")) {
            return HtmlBrowserSsoHandoffKind.Saml;
        }

        if (names.Contains("wresult") || names.Contains("wctx") || names.Contains("wa")) {
            return HtmlBrowserSsoHandoffKind.WsFederation;
        }

        if (names.Contains("id_token")) {
            return HtmlBrowserSsoHandoffKind.OpenIdConnect;
        }

        if (names.Contains("code") || names.Contains("access_token") || names.Contains("refresh_token") || names.Contains("error")) {
            return HtmlBrowserSsoHandoffKind.OAuth2;
        }

        return HtmlBrowserSsoHandoffKind.Unknown;
    }

    private static bool IsSensitiveSsoField(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return false;
        }

        string normalized = name.ToLowerInvariant();
        return SsoHandoffFieldNames.Contains(normalized)
            || HtmlSensitiveValueRedactor.IsSensitiveName(name);
    }

    private const string SsoHandoffScript = @"() => {
        const esc = (globalThis.CSS && CSS.escape) ? CSS.escape : (s) => String(s).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
        const selectorFor = (form, index) => {
            if (form.id) return `form#${esc(form.id)}`;
            if (form.name) return `form[name='${String(form.name).replace(/\\/g, '\\\\').replace(/'/g, ""\\'"")}']`;
            return `form:nth-of-type(${index + 1})`;
        };
        const forms = Array.from(document.forms || []).map((form, index) => ({
            index,
            selector: selectorFor(form, index),
            action: form.action || form.getAttribute('action') || '',
            method: String(form.method || form.getAttribute('method') || 'get').toUpperCase(),
            autoSubmitPrevented: form.getAttribute('data-htmltinkerx-sso-handoff') === 'true',
            fields: Array.from(form.elements || [])
                .filter(element => element && element.name)
                .map(element => ({
                    name: String(element.name || ''),
                    type: String(element.type || element.tagName || ''),
                    value: typeof element.value === 'string' ? element.value : ''
                }))
        }));

        return JSON.stringify({
            url: location.href,
            title: document.title || '',
            forms
        });
    }";
}
