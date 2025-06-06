using System;
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

        var settings = new NUglify.Css.CssSettings {
            DecodeEscapes = false
        };
        var result = NUglify.Uglify.Css(css, settings);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"OptimizeCss -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
    }
}
