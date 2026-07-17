using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Reads selected computed CSS properties from one rendered element.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="selector">CSS selector identifying the element.</param>
    /// <param name="propertyNames">CSS property names to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Computed values keyed by the requested property names.</returns>
    public static Task<IReadOnlyDictionary<string, string>> GetComputedStylesAsync(
        HtmlBrowserSession session,
        string selector,
        IEnumerable<string> propertyNames,
        CancellationToken cancellationToken = default) =>
        GetStyleValuesAsync(session, selector, propertyNames, cancellationToken);

    /// <summary>
    /// Reads selected CSS custom properties from one rendered element.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="selector">CSS selector identifying the element, typically <c>html</c>.</param>
    /// <param name="propertyNames">Custom property names, including their leading <c>--</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Computed values keyed by the requested custom property names.</returns>
    public static Task<IReadOnlyDictionary<string, string>> GetCssCustomPropertiesAsync(
        HtmlBrowserSession session,
        string selector,
        IEnumerable<string> propertyNames,
        CancellationToken cancellationToken = default) =>
        GetStyleValuesAsync(session, selector, propertyNames, cancellationToken);

    /// <summary>
    /// Reads one attribute from the first rendered element matching a selector.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="selector">CSS selector identifying the element.</param>
    /// <param name="attributeName">Attribute name to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attribute value, or <see langword="null"/> when it is absent.</returns>
    public static Task<string?> GetAttributeAsync(
        HtmlBrowserSession session,
        string selector,
        string attributeName,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("Selector cannot be empty.", nameof(selector));
        }

        if (string.IsNullOrWhiteSpace(attributeName)) {
            throw new ArgumentException("Attribute name cannot be empty.", nameof(attributeName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return session.Page.Locator(selector).First.GetAttributeAsync(attributeName);
    }

    /// <summary>
    /// Audits the current rendered DOM using the shared HtmlTinkerX document audit contract.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="options">Optional audit settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured issues discovered in the rendered document.</returns>
    public static async Task<HtmlDocumentAuditResult> AuditDocumentAsync(
        HtmlBrowserSession session,
        HtmlDocumentAuditOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string html = await session.Page.ContentAsync().ConfigureAwait(false);
        return HtmlDocumentAudit.Analyze(html, options);
    }

    private static async Task<IReadOnlyDictionary<string, string>> GetStyleValuesAsync(
        HtmlBrowserSession session,
        string selector,
        IEnumerable<string> propertyNames,
        CancellationToken cancellationToken) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("Selector cannot be empty.", nameof(selector));
        }

        if (propertyNames == null) {
            throw new ArgumentNullException(nameof(propertyNames));
        }

        string[] names = propertyNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (names.Length == 0) {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        cancellationToken.ThrowIfCancellationRequested();
        string json = await session.Page.Locator(selector).First.EvaluateAsync<string>(
            "(element, names) => JSON.stringify(Object.fromEntries(names.map(name => [name, getComputedStyle(element).getPropertyValue(name).trim()])))",
            names).ConfigureAwait(false);

        Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return values ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
