using System;

namespace HtmlTinkerX;

/// <summary>
/// Describes how a generated Select-HtmlData command should reopen its source.
/// </summary>
public sealed class HtmlDomCommandSource {
    /// <summary>URL source, when discovery downloaded a page.</summary>
    public Uri? Url { get; set; }

    /// <summary>File source, when discovery read a local snapshot.</summary>
    public string? Path { get; set; }

    /// <summary>PowerShell expression used for an in-memory source.</summary>
    public string ContentExpression { get; set; } = "$html";

    /// <summary>Optional base URL used to resolve relative links in content or file sources.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Request-specific user agent that must be replayed.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Request-specific proxy address that must be replayed.</summary>
    public string? Proxy { get; set; }

    /// <summary>Whether the generated command should reference the caller's Header variable.</summary>
    public bool UsesHeaders { get; set; }

    /// <summary>Whether the generated command should reference the caller's ProxyCredential variable.</summary>
    public bool UsesProxyCredential { get; set; }
}
