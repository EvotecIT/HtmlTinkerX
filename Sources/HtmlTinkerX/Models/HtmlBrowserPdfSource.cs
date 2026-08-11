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

    /// <summary>Gets the base URI normalized for browser resource resolution.</summary>
    internal Uri? ResourceBaseUri {
        get {
            if (BaseUri?.IsFile != true) return BaseUri;
            string path = HtmlBrowserFileSystemPath.GetValidatedLocalPath(BaseUri.LocalPath);
            if (!Directory.Exists(path)) return BaseUri;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                fullPath += Path.DirectorySeparatorChar;
            }
            return new Uri(fullPath);
        }
    }

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
            UriBuilder origin = new(value.Scheme, value.IdnHost, value.Port);
            return origin.Uri;
        }
    }

    /// <summary>Gets the navigation URI used to give an HTML string its declared HTTP origin.</summary>
    internal Uri? HtmlDocumentUri {
        get {
            if (Kind != HtmlBrowserPdfSourceKind.Html || BaseUri == null) return null;
            if (SecurityOrigin != null) {
                UriBuilder builder = new(BaseUri) { Fragment = string.Empty };
                return builder.Uri;
            }
            if (!BaseUri.IsFile || FileBaseDirectory == null) return null;
            return new Uri(Path.Combine(FileBaseDirectory, ".htmltinkerx-document.html"));
        }
    }

    /// <summary>Gets the trusted local directory used by an HTML-string file base.</summary>
    internal string? FileBaseDirectory {
        get {
            if (Kind != HtmlBrowserPdfSourceKind.Html || BaseUri?.IsFile != true) return null;
            string path = HtmlBrowserFileSystemPath.GetValidatedLocalPath(BaseUri.LocalPath);
            return Directory.Exists(path) ? path : Path.GetDirectoryName(path);
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
        if (baseUri?.IsFile == true) {
            if (HtmlBrowserFileSystemPath.IsNetworkOrDevicePath(baseUri.AbsoluteUri)) {
                throw new ArgumentException("File base URI must use a direct local path.", nameof(baseUri));
            }
            _ = HtmlBrowserFileSystemPath.GetValidatedLocalPath(baseUri.LocalPath);
        }

        return new HtmlBrowserPdfSource(HtmlBrowserPdfSourceKind.Html, null, html, null, baseUri);
    }

    /// <summary>Creates a local HTML-file source.</summary>
    public static HtmlBrowserPdfSource FromFile(string path) {
        return new HtmlBrowserPdfSource(
            HtmlBrowserPdfSourceKind.File,
            null,
            null,
            HtmlBrowserFileSystemPath.GetValidatedLocalPath(path),
            null);
    }
}
