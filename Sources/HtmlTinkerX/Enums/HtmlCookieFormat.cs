namespace HtmlTinkerX;

/// <summary>
/// Describes input formats for <see cref="HtmlCookieParser"/>.
/// </summary>
public enum HtmlCookieFormat {
    /// <summary>Netscape HTTP cookie file format.</summary>
    Netscape,
    /// <summary>HTTP Set-Cookie header.</summary>
    SetCookie,
    /// <summary>org.json cookie JSON or string.</summary>
    OrgJson,
    /// <summary>Chrome CookieStore JSON format.</summary>
    CookieStore,
    /// <summary>Puppeteer cookie JSON array.</summary>
    Puppeteer
}
