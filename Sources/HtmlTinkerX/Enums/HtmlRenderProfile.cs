namespace HtmlTinkerX;

/// <summary>
/// Preset rendering strategies for common browser extraction scenarios.
/// </summary>
public enum HtmlRenderProfile {
    /// <summary>Use explicit caller-provided rendering options only.</summary>
    Custom = 0,

    /// <summary>Use fast, parsing-friendly defaults for JavaScript-heavy pages that often keep background requests open.</summary>
    HeavyDynamicPage = 1,

    /// <summary>Use quick defaults for mostly static pages where subresources are not needed for extraction.</summary>
    FastStaticFallback = 2,

    /// <summary>Use forgiving defaults for pages that need visible clicks, short hydration waits, and paced interactions.</summary>
    InteractivePage = 3,

    /// <summary>Use defaults for pages that reveal content while scrolling after initial hydration.</summary>
    LazyLoadedPage = 4,

    /// <summary>Alias for <see cref="LazyLoadedPage"/>.</summary>
    LazyLoadedContent = LazyLoadedPage,

    /// <summary>Use defaults for JavaScript application shells that hydrate content after the initial document commit.</summary>
    AppShell = 5,

    /// <summary>Use conservative defaults for login-protected pages where styling and scripts may be required.</summary>
    LoginProtected = 6,

    /// <summary>Use network-observability defaults without blocking subresources.</summary>
    NetworkCapture = 7,

    /// <summary>Use bandwidth-saving defaults that block heavy visual resources before navigation.</summary>
    LowBandwidth = 8
}
