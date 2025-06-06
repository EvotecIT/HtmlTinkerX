using System;
using NUglify;
using NUglify.Html;
using System.Linq;

namespace PSParseHTML;

/// <summary>
/// Helper methods for optimizing markup resources.
/// </summary>
public static class HtmlOptimizer {
    /// <summary>
    /// Minifies the provided CSS using NUglify.
    /// </summary>
    /// <param name="css">CSS code to optimize.</param>
    /// <returns>Minified CSS string.</returns>
    public static string OptimizeCss(string css) {
        if (css == null) {
            throw new ArgumentNullException(nameof(css));
        }

        var settings = new NUglify.Css.CssSettings { DecodeEscapes = false };
        var result = NUglify.Uglify.Css(css, settings);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"OptimizeCss -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
    }

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

        HtmlSettings settings = new() { RemoveOptionalTags = false, RemoveComments = true };
        settings.CssSettings.DecodeEscapes = cssDecodeEscapes;

        bool hasMotw =
            html.IndexOf("<!-- saved from url=(0014)about:internet -->", StringComparison.OrdinalIgnoreCase) >= 0;

        UglifyResult result = Uglify.Html(html, settings);
        string output = result.Code ?? string.Empty;

        if (hasMotw) {
            return "<!-- saved from url=(0014)about:internet -->" + Environment.NewLine + output;
        }

        return output;
    }

    /// <summary>
    /// Minifies JavaScript code using NUglify.
    /// </summary>
    /// <param name="js">JavaScript code to optimize.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="js"/> is null.</exception>
    public static string OptimizeJavaScript(string js) {
        if (js == null) {
            throw new ArgumentNullException(nameof(js));
        }

        var result = NUglify.Uglify.Js(js);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"Optimize-JavaScript -Errors: {errors}");
        }

        return result.Code ?? string.Empty;
    }
}
