namespace HtmlTinkerX;

using System;
using System.IO;

/// <summary>Immutable input for a browser-backed PDF capture.</summary>
public sealed class HtmlBrowserPdfSource {
    private HtmlBrowserPdfSource(HtmlBrowserPdfSourceKind kind, Uri? uri, string? html, string? filePath, Uri? baseUri) {
        Kind = kind;
        Uri = uri;
        Html = html;
        FilePath = filePath;
        BaseUri = baseUri;
    }

    /// <summary>Gets the input kind.</summary>
    public HtmlBrowserPdfSourceKind Kind { get; }

    /// <summary>Gets the absolute URL for URL input.</summary>
    public Uri? Uri { get; }

    /// <summary>Gets the markup for HTML-string input.</summary>
    public string? Html { get; }

    /// <summary>Gets the normalized absolute path for file input.</summary>
    public string? FilePath { get; }

    /// <summary>Gets the optional base URI used to resolve HTML-string resources.</summary>
    public Uri? BaseUri { get; }

    /// <summary>Gets the HTTP origin to which per-render headers and web storage may be scoped.</summary>
    internal Uri? SecurityOrigin {
        get {
            Uri? value = Kind switch {
                HtmlBrowserPdfSourceKind.Url => Uri,
                HtmlBrowserPdfSourceKind.Html => BaseUri,
                _ => null
            };
            if (value == null || (value.Scheme != System.Uri.UriSchemeHttp && value.Scheme != System.Uri.UriSchemeHttps)) {
                return null;
            }
            UriBuilder origin = new(value.Scheme, value.Host, value.Port);
            return origin.Uri;
        }
    }

    /// <summary>Gets the navigation URI used to give an HTML string its declared HTTP origin.</summary>
    internal Uri? HtmlDocumentUri {
        get {
            if (Kind != HtmlBrowserPdfSourceKind.Html || SecurityOrigin == null) return null;
            UriBuilder builder = new(BaseUri!) { Fragment = string.Empty };
            return builder.Uri;
        }
    }

    /// <summary>Creates a URL source.</summary>
    public static HtmlBrowserPdfSource FromUrl(string url) {
        if (string.IsNullOrWhiteSpace(url)) {
            throw new ArgumentException("URL cannot be empty.", nameof(url));
        }

        if (!System.Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        return FromUrl(uri);
    }

    /// <summary>Creates a URL source.</summary>
    public static HtmlBrowserPdfSource FromUrl(Uri url) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        if (!url.IsAbsoluteUri) {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        return new HtmlBrowserPdfSource(HtmlBrowserPdfSourceKind.Url, url, null, null, null);
    }

    /// <summary>Creates an HTML-string source with an optional resource base URI.</summary>
    public static HtmlBrowserPdfSource FromHtml(string html, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (baseUri != null && !baseUri.IsAbsoluteUri) {
            throw new ArgumentException("Base URI must be absolute.", nameof(baseUri));
        }

        return new HtmlBrowserPdfSource(HtmlBrowserPdfSourceKind.Html, null, html, null, baseUri);
    }

    /// <summary>Creates a local HTML-file source.</summary>
    public static HtmlBrowserPdfSource FromFile(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("File path cannot be empty.", nameof(path));
        }

        return new HtmlBrowserPdfSource(HtmlBrowserPdfSourceKind.File, null, null, Path.GetFullPath(path), null);
    }
}
