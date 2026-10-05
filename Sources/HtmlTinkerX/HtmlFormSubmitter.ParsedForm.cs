using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlFormSubmitter {
    /// <summary>Submits a parsed form's successful values using its resolved HTTP action.</summary>
    /// <param name="form">Parsed form, including ordered successful values and its document address.</param>
    /// <param name="fieldOverrides">Optional replacements by ordinal field name. Repeated replacements stay in their supplied order.</param>
    /// <param name="client">Optional reusable HTTP client, owned by the caller. Reuse a cookie-enabled client to retain the page's session.</param>
    /// <param name="fetchOptions">Response byte policy; null uses the default 16 MiB limit.</param>
    /// <param name="cancellationToken">Cancellation covering request headers and the response body.</param>
    /// <returns>Decoded HTML response.</returns>
    /// <remarks>
    /// Overrides replace every existing value for that name at its first occurrence; new names are appended.
    /// GET replaces the action query with the submitted values, as an HTML form does. POST retains the action query.
    /// This submits URL-encoded values; it does not run browser validation, scripts, or file upload handling.
    /// </remarks>
    public static Task<string> SubmitAsync(HtmlFormResult form, IEnumerable<KeyValuePair<string, string>>? fieldOverrides = null,
        HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (form == null) throw new ArgumentNullException(nameof(form));
        cancellationToken.ThrowIfCancellationRequested();
        Uri? action = form.Metadata.ResolvedActionUri;
        if (action == null) Uri.TryCreate(form.Metadata.Action, UriKind.Absolute, out action);
        if (action == null || !action.IsAbsoluteUri || (action.Scheme != Uri.UriSchemeHttp && action.Scheme != Uri.UriSchemeHttps)) {
            throw new ArgumentException("The form needs an absolute HTTP action. Supply the document address when parsing relative or empty actions.", nameof(form));
        }
        if (form.Metadata.Method == FormMethod.Get) {
            action = new UriBuilder(action) { Query = string.Empty }.Uri;
        }
        List<KeyValuePair<string, string>> values = MergeFieldOverrides(form.SuccessfulFields, fieldOverrides);
        return SubmitAsync(action.AbsoluteUri, form.Metadata.Method, values, client, fetchOptions, cancellationToken);
    }

    private static List<KeyValuePair<string, string>> MergeFieldOverrides(IEnumerable<KeyValuePair<string, string>> defaults,
        IEnumerable<KeyValuePair<string, string>>? overrides) {
        Dictionary<string, List<KeyValuePair<string, string>>> replacements = new(StringComparer.Ordinal);
        List<string> replacementOrder = new();
        if (overrides != null) {
            foreach (KeyValuePair<string, string> value in overrides) {
                if (!replacements.TryGetValue(value.Key, out List<KeyValuePair<string, string>>? group)) {
                    group = new();
                    replacements.Add(value.Key, group);
                    replacementOrder.Add(value.Key);
                }
                group.Add(value);
            }
        }
        List<KeyValuePair<string, string>> result = new();
        HashSet<string> applied = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> value in defaults) {
            if (replacements.TryGetValue(value.Key, out List<KeyValuePair<string, string>>? replacement)) {
                if (applied.Add(value.Key)) result.AddRange(replacement);
            } else result.Add(value);
        }
        foreach (string name in replacementOrder) {
            if (applied.Add(name)) result.AddRange(replacements[name]);
        }
        return result;
    }
}
