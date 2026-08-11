namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>Immutable, per-render browser PDF request.</summary>
public sealed class HtmlBrowserPdfRequest {
    /// <summary>Initializes a browser PDF request.</summary>
    public HtmlBrowserPdfRequest(
        HtmlBrowserPdfSource source,
        HtmlBrowserPdfOptions? pdfOptions = null,
        HtmlBrowserPdfReadiness? readiness = null,
        IReadOnlyDictionary<string, string>? headers = null,
        IEnumerable<HtmlBrowserPdfCookie>? cookies = null,
        IReadOnlyDictionary<string, string>? localStorage = null,
        IReadOnlyDictionary<string, string>? sessionStorage = null,
        string? styleSheetContent = null,
        string? beforeCaptureScript = null,
        bool bypassContentSecurityPolicy = false,
        HtmlBrowserPdfMediaType mediaType = HtmlBrowserPdfMediaType.Print,
        int navigationTimeout = 30000,
        int beforeCaptureScriptTimeout = 30000,
        int pdfTimeout = 30000) {
        if (navigationTimeout < 0) throw new ArgumentOutOfRangeException(nameof(navigationTimeout));
        if (beforeCaptureScriptTimeout < 0) throw new ArgumentOutOfRangeException(nameof(beforeCaptureScriptTimeout));
        if (pdfTimeout < 0) throw new ArgumentOutOfRangeException(nameof(pdfTimeout));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        PdfOptions = pdfOptions ?? new HtmlBrowserPdfOptions();
        Readiness = readiness ?? new HtmlBrowserPdfReadiness();
        Headers = Snapshot(headers, StringComparer.OrdinalIgnoreCase);
        Cookies = Array.AsReadOnly((cookies ?? Array.Empty<HtmlBrowserPdfCookie>()).ToArray());
        LocalStorage = Snapshot(localStorage, StringComparer.Ordinal);
        SessionStorage = Snapshot(sessionStorage, StringComparer.Ordinal);
        if ((Headers.Count != 0 || LocalStorage.Count != 0 || SessionStorage.Count != 0) && Source.SecurityOrigin == null) {
            throw new ArgumentException(
                "Per-render headers and web storage require a URL source or an HTML source with an absolute HTTP/HTTPS base URI so credentials can be limited to one origin.",
                nameof(source));
        }
        StyleSheetContent = styleSheetContent;
        BeforeCaptureScript = beforeCaptureScript;
        BypassContentSecurityPolicy = bypassContentSecurityPolicy;
        MediaType = mediaType;
        NavigationTimeout = navigationTimeout;
        BeforeCaptureScriptTimeout = beforeCaptureScriptTimeout;
        PdfTimeout = pdfTimeout;
    }

    /// <summary>Gets the capture source.</summary>
    public HtmlBrowserPdfSource Source { get; }
    /// <summary>Gets the Chromium PDF print options.</summary>
    public HtmlBrowserPdfOptions PdfOptions { get; }
    /// <summary>Gets the readiness conditions.</summary>
    public HtmlBrowserPdfReadiness Readiness { get; }
    /// <summary>Gets extra headers applied only to HTTP(S) page, popup, dedicated worker, and subresource requests matching the source origin.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Gets cookies applied before navigation.</summary>
    public IReadOnlyList<HtmlBrowserPdfCookie> Cookies { get; }
    /// <summary>Gets local-storage values applied before page scripts run, only for the source origin.</summary>
    public IReadOnlyDictionary<string, string> LocalStorage { get; }
    /// <summary>Gets session-storage values applied before page scripts run, only for the source origin.</summary>
    public IReadOnlyDictionary<string, string> SessionStorage { get; }
    /// <summary>Gets optional CSS injected after navigation.</summary>
    public string? StyleSheetContent { get; }
    /// <summary>Gets optional JavaScript evaluated immediately before readiness checks.</summary>
    public string? BeforeCaptureScript { get; }
    /// <summary>Gets whether the isolated context bypasses page Content Security Policy checks.</summary>
    public bool BypassContentSecurityPolicy { get; }
    /// <summary>Gets the CSS media type emulated before printing.</summary>
    public HtmlBrowserPdfMediaType MediaType { get; }
    /// <summary>Gets the timeout in milliseconds for initial source navigation or content loading. Zero disables the timeout.</summary>
    public int NavigationTimeout { get; }
    /// <summary>Gets the timeout in milliseconds for the optional pre-capture script. Zero disables the timeout.</summary>
    public int BeforeCaptureScriptTimeout { get; }
    /// <summary>Gets the timeout in milliseconds for Chromium PDF generation. Zero disables the timeout.</summary>
    public int PdfTimeout { get; }

    private static IReadOnlyDictionary<string, string> Snapshot(IReadOnlyDictionary<string, string>? values, StringComparer comparer) {
        Dictionary<string, string> copy = new(comparer);
        if (values != null) {
            foreach (KeyValuePair<string, string> item in values) {
                if (string.IsNullOrWhiteSpace(item.Key)) {
                    throw new ArgumentException("Dictionary keys cannot be empty.", nameof(values));
                }
                copy[item.Key] = item.Value ?? string.Empty;
            }
        }
        return new ReadOnlyDictionary<string, string>(copy);
    }
}
