using System;

namespace HtmlTinkerX;

/// <summary>A page an <see cref="HtmlFetchPolicy.Renderer"/> produced: its HTML and the address it finally showed.</summary>
public sealed class HtmlRenderedPage {
    /// <summary>Creates a rendered page.</summary>
    /// <param name="html">The rendered HTML.</param>
    /// <param name="finalUri">The address after any redirects or script navigation, or null when it is the requested one.</param>
    public HtmlRenderedPage(string html, Uri? finalUri = null) {
        Html = html ?? throw new ArgumentNullException(nameof(html));
        if (finalUri != null && !finalUri.IsAbsoluteUri) throw new ArgumentException("An absolute address is required.", nameof(finalUri));
        FinalUri = finalUri;
    }

    /// <summary>The rendered HTML.</summary>
    public string Html { get; }

    /// <summary>The address after any redirects or script navigation, or null when it is the requested one.</summary>
    public Uri? FinalUri { get; }
}
