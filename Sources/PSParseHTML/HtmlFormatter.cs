using System;
using System.IO;
using AngleSharp.Css.Parser;
using AngleSharp.Css;

namespace PSParseHTML;

/// <summary>
/// Helper methods for formatting markup and style sheets.
/// </summary>
public static class HtmlFormatter {
    /// <summary>
    /// Formats CSS using AngleSharp's <see cref="CssParser"/> and <see cref="CssStyleFormatter"/>.
    /// </summary>
    /// <param name="css">CSS content to format.</param>
    /// <returns>Formatted CSS string.</returns>
    public static string FormatCss(string css) {
        if (css == null) {
            throw new ArgumentNullException(nameof(css));
        }

        var parser = new CssParser();
        var sheet = parser.ParseStyleSheet(css);
        using var writer = new StringWriter();
        var formatter = new CssStyleFormatter();
        sheet.ToCss(writer, formatter);
        return writer.ToString();
    }
}
