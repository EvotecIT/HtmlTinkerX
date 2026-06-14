namespace HtmlTinkerX;

/// <summary>
/// Preset rendering strategies for common browser extraction scenarios.
/// </summary>
public enum HtmlRenderProfile {
    /// <summary>Use explicit caller-provided rendering options only.</summary>
    Custom,

    /// <summary>Use fast, parsing-friendly defaults for JavaScript-heavy pages that often keep background requests open.</summary>
    HeavyDynamicPage
}
