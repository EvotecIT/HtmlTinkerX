using System;
using NUglify;
using NUglify.Html;

namespace PSParseHTML;

/// <summary>
/// Provides HTML optimization utilities using NUglify.
/// </summary>
public static class HtmlOptimizer {
    /// <summary>
    /// Minifies the provided HTML content.
    /// </summary>
    /// <param name="html">HTML string to optimize.</param>
    /// <param name="cssDecodeEscapes">Whether to decode CSS escape sequences.</param>
    /// <returns>Optimized HTML output.</returns>
    public static string OptimizeHtml(string html, bool cssDecodeEscapes) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlSettings settings = new() {
            RemoveOptionalTags = false,
            RemoveComments = true
        };
        settings.CssSettings.DecodeEscapes = cssDecodeEscapes;

        bool hasMotw = html.IndexOf("<!-- saved from url=(0014)about:internet -->", StringComparison.OrdinalIgnoreCase) >= 0;

        UglifyResult result = Uglify.Html(html, settings);
        string output = result.Code ?? string.Empty;

        if (hasMotw) {
            return "<!-- saved from url=(0014)about:internet -->" + Environment.NewLine + output;
        }
        return output;
    }
}
