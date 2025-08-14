using Microsoft.Playwright;

namespace HtmlTinkerX;

/// <summary>
/// Represents a browser cookie.
/// </summary>
public sealed class HtmlCookie {
    /// <summary>Name of the cookie.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Cookie value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional URL for the cookie.</summary>
    public string? Url { get; set; }

    /// <summary>Cookie domain.</summary>
    public string? Domain { get; set; }

    /// <summary>Cookie path.</summary>
    public string? Path { get; set; }

    /// <summary>Expiration time as UNIX timestamp.</summary>
    public long? Expires { get; set; }

    /// <summary>True when the cookie is HTTP only.</summary>
    public bool? HttpOnly { get; set; }

    /// <summary>True when the cookie requires HTTPS.</summary>
    public bool? Secure { get; set; }

    /// <summary>SameSite policy.</summary>
    public SameSiteAttribute? SameSite { get; set; }
}