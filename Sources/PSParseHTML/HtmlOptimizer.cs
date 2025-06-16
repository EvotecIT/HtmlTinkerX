using System;
using System.IO;
using NUglify;
using NUglify.Html;
using System.Linq;
using System.Threading.Tasks;

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
    /// Asynchronously minifies the provided CSS using NUglify.
    /// </summary>
    /// <param name="css">CSS code to optimize.</param>
    /// <returns>Minified CSS string.</returns>
    public static Task<string> OptimizeCssAsync(string css)
        => Task.Run(() => OptimizeCss(css));

    /// <summary>
    /// Minifies CSS from a file.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Minified CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeCssFile(string filePath) {
        string css = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeCss(css);
    }

    /// <summary>
    /// Asynchronously minifies CSS from a file.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Minified CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> OptimizeCssFileAsync(string filePath) {
        string css = await HtmlUtilities.ReadFileCheckedAsync(filePath).ConfigureAwait(false);
        return await OptimizeCssAsync(css).ConfigureAwait(false);
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
    /// Asynchronously minifies the provided HTML content.
    /// </summary>
    /// <inheritdoc cref="OptimizeHtml(string,bool)"/>
    public static Task<string> OptimizeHtmlAsync(string html, bool cssDecodeEscapes)
        => Task.Run(() => OptimizeHtml(html, cssDecodeEscapes));

    /// <summary>
    /// Minifies HTML content from a file.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="cssDecodeEscapes">Whether to decode CSS escape sequences.</param>
    /// <returns>Optimized HTML output.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeHtmlFile(string filePath, bool cssDecodeEscapes) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeHtml(html, cssDecodeEscapes);
    }

    /// <summary>
    /// Asynchronously minifies HTML content from a file.
    /// </summary>
    /// <inheritdoc cref="OptimizeHtmlFile(string,bool)"/>
    public static async Task<string> OptimizeHtmlFileAsync(string filePath, bool cssDecodeEscapes) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(filePath).ConfigureAwait(false);
        return await OptimizeHtmlAsync(html, cssDecodeEscapes).ConfigureAwait(false);
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

    /// <summary>
    /// Asynchronously minifies JavaScript code using NUglify.
    /// </summary>
    /// <param name="js">JavaScript code to optimize.</param>
    /// <returns>Minified JavaScript.</returns>
    public static Task<string> OptimizeJavaScriptAsync(string js)
        => Task.Run(() => OptimizeJavaScript(js));

    /// <summary>
    /// Minifies JavaScript code from a file using NUglify.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeJavaScriptFile(string filePath) {
        string js = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeJavaScript(js);
    }

    /// <summary>
    /// Asynchronously minifies JavaScript code from a file using NUglify.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> OptimizeJavaScriptFileAsync(string filePath) {
        string js = await HtmlUtilities.ReadFileCheckedAsync(filePath).ConfigureAwait(false);
        return await OptimizeJavaScriptAsync(js).ConfigureAwait(false);
    }
}
