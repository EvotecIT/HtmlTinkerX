namespace HtmlTinkerX;

/// <summary>SameSite behavior for a browser-context cookie.</summary>
public enum HtmlBrowserCookieSameSite {
    /// <summary>Send the cookie on same-site requests and top-level cross-site navigation.</summary>
    Lax,
    /// <summary>Send the cookie only on same-site requests.</summary>
    Strict,
    /// <summary>Send the cookie on same-site and cross-site requests.</summary>
    None
}
