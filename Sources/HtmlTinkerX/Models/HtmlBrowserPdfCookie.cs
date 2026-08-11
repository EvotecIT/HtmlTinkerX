namespace HtmlTinkerX;

using System;

/// <summary>Immutable cookie injected into an isolated PDF capture context.</summary>
public sealed class HtmlBrowserPdfCookie {
    /// <summary>Initializes a cookie.</summary>
    public HtmlBrowserPdfCookie(
        string name,
        string value,
        string? url = null,
        string? domain = null,
        string? path = null,
        long? expires = null,
        bool? httpOnly = null,
        bool? secure = null,
        HtmlBrowserCookieSameSite? sameSite = null) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cookie name cannot be empty.", nameof(name));
        if (value == null) throw new ArgumentNullException(nameof(value));
        string? normalizedUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        string? normalizedDomain = string.IsNullOrWhiteSpace(domain) ? null : domain;
        string? normalizedPath = string.IsNullOrWhiteSpace(path) ? null : path;
        if (normalizedUrl == null && normalizedDomain == null) {
            throw new ArgumentException("A cookie requires either a URL or a domain.", nameof(url));
        }
        if (normalizedUrl != null && (normalizedDomain != null || normalizedPath != null)) {
            throw new ArgumentException("A cookie URL cannot be combined with domain or path scope. Use either URL or a domain/path pair.", nameof(url));
        }

        Name = name;
        Value = value;
        Url = normalizedUrl;
        Domain = normalizedDomain;
        Path = normalizedUrl == null && normalizedDomain != null && normalizedPath == null
            ? "/"
            : normalizedPath;
        Expires = expires;
        HttpOnly = httpOnly;
        Secure = secure;
        SameSite = sameSite;
    }

    /// <summary>Gets the cookie name.</summary>
    public string Name { get; }
    /// <summary>Gets the cookie value.</summary>
    public string Value { get; }
    /// <summary>Gets the optional cookie URL.</summary>
    public string? Url { get; }
    /// <summary>Gets the optional cookie domain.</summary>
    public string? Domain { get; }
    /// <summary>Gets the cookie path.</summary>
    public string? Path { get; }
    /// <summary>Gets the UNIX expiration timestamp.</summary>
    public long? Expires { get; }
    /// <summary>Gets whether the cookie is HTTP-only.</summary>
    public bool? HttpOnly { get; }
    /// <summary>Gets whether the cookie requires HTTPS.</summary>
    public bool? Secure { get; }
    /// <summary>Gets the SameSite policy.</summary>
    public HtmlBrowserCookieSameSite? SameSite { get; }
}
