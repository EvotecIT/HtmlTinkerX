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
        HtmlBrowserPdfMediaType mediaType = HtmlBrowserPdfMediaType.Print) {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        PdfOptions = pdfOptions ?? new HtmlBrowserPdfOptions();
        Readiness = readiness ?? new HtmlBrowserPdfReadiness();
        Headers = Snapshot(headers);
        Cookies = Array.AsReadOnly((cookies ?? Array.Empty<HtmlBrowserPdfCookie>()).ToArray());
        LocalStorage = Snapshot(localStorage);
        SessionStorage = Snapshot(sessionStorage);
        StyleSheetContent = styleSheetContent;
        BeforeCaptureScript = beforeCaptureScript;
        BypassContentSecurityPolicy = bypassContentSecurityPolicy;
        MediaType = mediaType;
    }

    /// <summary>Gets the capture source.</summary>
    public HtmlBrowserPdfSource Source { get; }
    /// <summary>Gets the Chromium PDF print options.</summary>
    public HtmlBrowserPdfOptions PdfOptions { get; }
    /// <summary>Gets the readiness conditions.</summary>
    public HtmlBrowserPdfReadiness Readiness { get; }
    /// <summary>Gets extra HTTP headers applied to the isolated context.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Gets cookies applied before navigation.</summary>
    public IReadOnlyList<HtmlBrowserPdfCookie> Cookies { get; }
    /// <summary>Gets local-storage values applied before page scripts run.</summary>
    public IReadOnlyDictionary<string, string> LocalStorage { get; }
    /// <summary>Gets session-storage values applied before page scripts run.</summary>
    public IReadOnlyDictionary<string, string> SessionStorage { get; }
    /// <summary>Gets optional CSS injected after navigation.</summary>
    public string? StyleSheetContent { get; }
    /// <summary>Gets optional JavaScript evaluated immediately before readiness checks.</summary>
    public string? BeforeCaptureScript { get; }
    /// <summary>Gets whether the isolated context bypasses page Content Security Policy checks.</summary>
    public bool BypassContentSecurityPolicy { get; }
    /// <summary>Gets the CSS media type emulated before printing.</summary>
    public HtmlBrowserPdfMediaType MediaType { get; }

    private static IReadOnlyDictionary<string, string> Snapshot(IReadOnlyDictionary<string, string>? values) {
        Dictionary<string, string> copy = new(StringComparer.OrdinalIgnoreCase);
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
